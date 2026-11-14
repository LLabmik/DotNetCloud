using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Sdk.Tests;

/// <summary>
/// Guards the bridge reference every module widget document ships.
/// </summary>
/// <remarks>
/// <para>
/// A widget frame is an opaque-origin sandbox, so it carries its identity in a short-lived frame
/// ticket (<c>?dncTicket=…</c>) that <c>dncModuleBridge.js</c> copies onto every <c>fetch</c> as the
/// <c>dnc-frame-ticket</c> header. If the widget document does not load that file, the frame has no
/// ticket, every API call is answered 401 by the module proxy, and the widget falls back to its
/// generic "unable to load" text — which is exactly what the Bookmarks card showed.
/// </para>
/// <para>
/// The only bridge path that is guaranteed to exist is <c>../js/dncModuleBridge.js</c>: each module's
/// <c>.Web</c> project links the SDK's single copy there. A bare <c>../dncModuleBridge.js</c> happens
/// to resolve in the modules whose host also receives the SDK package's <c>contentFiles</c> copy at
/// the wwwroot root, which is an accident of packaging, not a contract.
/// </para>
/// </remarks>
[TestClass]
public class WidgetFrameAssetsTests
{
    private const string BridgeScript = "../js/dncModuleBridge.js";

    private static readonly Regex ScriptSource = new(
        "<script[^>]*\\bsrc\\s*=\\s*\"(?<src>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AnyReference = new(
        "(?:src|href)\\s*=\\s*\"(?<ref>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Every widget document that ships with a module must load the bridge from the path its module
    /// publishes, and must not reference anything outside its own framed directory.
    /// </summary>
    [TestMethod]
    public void EveryWidgetDocument_LoadsTheBridgeFromThePathTheModulePublishes()
    {
        var repoRoot = FindRepositoryRoot();
        var moduleRoot = Path.Combine(repoRoot, "src", "Modules");
        var widgets = Directory
            .EnumerateFiles(moduleRoot, "index.html", SearchOption.AllDirectories)
            .Where(path => Normalize(path).Contains("/wwwroot/widget/", StringComparison.Ordinal))
            .ToArray();

        Assert.IsTrue(widgets.Length >= 4, $"Expected the module widgets, found {widgets.Length} document(s).");

        var failures = new List<string>();

        foreach (var widget in widgets)
        {
            var relative = Path.GetRelativePath(repoRoot, widget).Replace('\\', '/');
            var html = File.ReadAllText(widget);

            foreach (Match match in AnyReference.Matches(html))
            {
                var reference = match.Groups["ref"].Value;
                if (reference.StartsWith('/')
                    || reference.Contains("/apps/", StringComparison.OrdinalIgnoreCase)
                    || reference.Contains("://", StringComparison.Ordinal))
                {
                    failures.Add($"{relative}: '{reference}' must be relative to the framed document.");
                }
            }

            var scripts = ScriptSource.Matches(html)
                .Select(match => match.Groups["src"].Value)
                .ToArray();

            if (!scripts.Contains(BridgeScript, StringComparer.Ordinal))
            {
                failures.Add(
                    $"{relative}: the widget must load the bridge as '{BridgeScript}' (found: {string.Join(", ", scripts)}).");
                continue;
            }

            var moduleName = ModuleFolderName(repoRoot, widget);
            var webProject = Path.Combine(
                moduleRoot, moduleName, $"DotNetCloud.Modules.{moduleName}.Web",
                $"DotNetCloud.Modules.{moduleName}.Web.csproj");

            if (!File.Exists(webProject))
            {
                failures.Add($"{relative}: no '{Path.GetFileName(webProject)}' to publish '{BridgeScript}'.");
                continue;
            }

            var project = File.ReadAllText(webProject);
            if (!Regex.IsMatch(project, "Link=\"wwwroot[\\\\/]js[\\\\/]dncModuleBridge\\.js\""))
            {
                failures.Add(
                    $"{Path.GetRelativePath(repoRoot, webProject).Replace('\\', '/')}: the module must publish the bridge at wwwroot/js/dncModuleBridge.js.");
            }
        }

        Assert.AreEqual(
            0,
            failures.Count,
            "Widget documents and their module web projects disagree about the bridge:"
            + string.Concat(failures.Select(failure => $"{Environment.NewLine}  {failure}")));
    }

    /// <summary>
    /// Derives the module folder name (for example <c>Bookmarks</c>) from a widget path such as
    /// <c>src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/wwwroot/widget/index.html</c>.
    /// </summary>
    private static string ModuleFolderName(string repoRoot, string widgetPath)
    {
        var relative = Path.GetRelativePath(Path.Combine(repoRoot, "src", "Modules"), widgetPath);
        var separator = relative.IndexOfAny(['/', '\\']);
        Assert.IsTrue(separator > 0, $"Could not derive the module folder from '{widgetPath}'.");
        return relative[..separator];
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string FindRepositoryRoot()
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

        Assert.Fail($"Could not locate DotNetCloud.sln above {AppContext.BaseDirectory}.");
        return string.Empty;
    }
}
