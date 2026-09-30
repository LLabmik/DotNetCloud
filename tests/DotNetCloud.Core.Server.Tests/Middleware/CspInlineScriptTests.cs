using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNetCloud.Core.ServiceDefaults.Middleware;

namespace DotNetCloud.Core.Server.Tests.Middleware;

/// <summary>
/// Guards the CSP inline-script allow-list in <see cref="CspPolicy"/> against silent drift.
/// </summary>
/// <remarks>
/// The CSP allow-lists the app's static inline <c>&lt;script&gt;</c> blocks by the SHA-256 of their
/// <b>exact rendered text</b>. Reformatting one of those blocks (indentation, line breaks) changes the
/// hash, so the browser starts <b>blocking</b> the script and whatever it bootstraps dies silently.
/// That is exactly how <c>window.blazorCulture</c> broke on 2026-09-27: the WASM client reads it at
/// startup (<c>DotNetCloud.UI.Web.Client/Program.cs</c>) and the layout's <c>CultureSelector</c> writes
/// it, so every page using <c>InteractiveAuto</c> stalled with a CSP violation in the console.
/// These tests recompute the hashes from the razor sources so a reformat fails the build instead.
/// </remarks>
[TestClass]
public sealed class CspInlineScriptTests
{
    private const string InlineScriptMismatchMessage =
        "CSP inline-script hash mismatch. Reformatting an inline <script> changes its SHA-256 and the " +
        "browser will block it. Update CspPolicy.ScriptHashes AND " +
        "appsettings.json Security:SecurityHeaders:ContentSecurityPolicy for: ";

    [TestMethod]
    public void InlineRazorScripts_EveryHash_IsAllowListedByCspPolicy()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var allowList = ExtractScriptHashes(CspPolicy.Default);
        var scripts = FindInlineScripts(root!);

        Assert.IsTrue(scripts.Count > 0, "Expected at least one inline <script> block in the razor sources.");

        var offenders = scripts
            .Where(script => !allowList.Contains(script.Hash))
            .Select(script => $"{script.RelativePath}:{script.Line} sha256-{script.Hash}")
            .ToList();

        Assert.AreEqual(0, offenders.Count, InlineScriptMismatchMessage + string.Join("; ", offenders));
    }

    [TestMethod]
    public void CspPolicy_AllowList_HasNoStaleHashes()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var allowList = ExtractScriptHashes(CspPolicy.Default);
        var scripts = FindInlineScripts(root!);

        Assert.AreEqual(
            scripts.Count,
            allowList.Count,
            "CspPolicy allows a different number of inline-script hashes than the razor sources contain. " +
            "If a script was removed or replaced, drop its stale hash from CspPolicy.ScriptHashes and appsettings.json.");
    }

    [TestMethod]
    public void AppSettings_CspPolicy_AllowListsEveryInlineScriptHash()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        // appsettings.json overrides CspPolicy.Default at runtime, so the deployed header uses this value.
        var appSettingsPath = Path.Combine(root!, "src", "Core", "DotNetCloud.Core.Server", "appsettings.json");
        Assert.IsTrue(File.Exists(appSettingsPath), $"Missing {appSettingsPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
        var configured = document.RootElement
            .GetProperty("Security")
            .GetProperty("SecurityHeaders")
            .GetProperty("ContentSecurityPolicy")
            .GetString();
        Assert.IsNotNull(configured, "Security:SecurityHeaders:ContentSecurityPolicy is missing from appsettings.json.");

        var configuredHashes = ExtractScriptHashes(configured!);
        var scripts = FindInlineScripts(root!);

        var missing = scripts
            .Where(script => !configuredHashes.Contains(script.Hash))
            .Select(script => $"{script.RelativePath}:{script.Line} sha256-{script.Hash}")
            .ToList();

        Assert.AreEqual(0, missing.Count,
            "appsettings.json ContentSecurityPolicy does not allow-list: " + string.Join("; ", missing));
    }

    private static List<InlineScript> FindInlineScripts(string repositoryRoot)
    {
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var results = new List<InlineScript>();

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.razor", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (var (line, body) in ExtractInlineScriptBodies(text))
            {
                results.Add(new InlineScript(
                    Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/'),
                    line,
                    Sha256Base64(body)));
            }
        }

        return results;
    }

    /// <summary>Yields the line number and exact body of every inline (no <c>src</c>) script block.</summary>
    private static IEnumerable<(int Line, string Body)> ExtractInlineScriptBodies(string text)
    {
        const string openTag = "<script";
        const string closeTag = "</script>";
        var index = 0;

        while (index < text.Length)
        {
            var start = text.IndexOf(openTag, index, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                yield break;
            }

            var tagEnd = text.IndexOf('>', start);
            if (tagEnd < 0)
            {
                yield break;
            }

            var close = text.IndexOf(closeTag, tagEnd + 1, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
            {
                yield break;
            }

            var tag = text[start..(tagEnd + 1)];
            var body = text[(tagEnd + 1)..close];
            if (!tag.Contains("src", StringComparison.OrdinalIgnoreCase) && body.Trim().Length > 0)
            {
                yield return (1 + text.AsSpan(0, start).Count('\n'), body);
            }

            index = close + closeTag.Length;
        }
    }

    /// <summary>Extracts the base64 SHA-256 tokens from a CSP value (without the <c>sha256-</c> prefix).</summary>
    private static HashSet<string> ExtractScriptHashes(string contentSecurityPolicy)
    {
        const string prefix = "'sha256-";
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        while (true)
        {
            var start = contentSecurityPolicy.IndexOf(prefix, index, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            start += prefix.Length;
            var end = contentSecurityPolicy.IndexOf('\'', start);
            if (end < 0)
            {
                break;
            }

            hashes.Add(contentSecurityPolicy[start..end]);
            index = end;
        }

        return hashes;
    }

    private static string Sha256Base64(string value) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DotNetCloud.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record InlineScript(string RelativePath, int Line, string Hash);
}
