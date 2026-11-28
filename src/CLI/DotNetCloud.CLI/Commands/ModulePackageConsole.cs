using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.ModulePackaging;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Output and trusted-key helpers shared by the package verbs: <c>module pack</c>,
/// <c>module inspect</c>, <c>module keygen</c>, <c>module verify</c> and <c>module feed</c>.
/// </summary>
/// <remarks>
/// The package verbs used to live in a separate <c>dotnetcloud-pack</c> executable. They are now
/// verbs of this CLI so the tool that builds a package ships with the platform that verifies it.
/// </remarks>
internal static class ModulePackageConsole
{
    /// <summary>Writes an error line to standard error.</summary>
    /// <param name="message">The message.</param>
    public static void Error(string message) => ConsoleOutput.WriteError(message);

    /// <summary>Writes an information line to standard output, unformatted so it stays pipeable.</summary>
    /// <param name="message">The message.</param>
    public static void Info(string message) => Console.WriteLine(message);

    /// <summary>Writes a warning line to standard error.</summary>
    /// <param name="message">The message.</param>
    public static void Warn(string message) => ConsoleOutput.WriteWarning(message);

    /// <summary>Prints structured warnings and issues for a verification result.</summary>
    /// <param name="verification">The verification to report.</param>
    public static void ReportIssues(ModulePackageVerification verification)
    {
        foreach (var warning in verification.Warnings)
        {
            Warn($"[{warning.Code}] {warning.Message}");
        }

        foreach (var issue in verification.Issues)
        {
            Error($"[{issue.Code}] {issue.Message}");
        }
    }

    /// <summary>
    /// Builds the trusted-key map from <c>--key id=base64</c> values and <c>--key-file</c> paths.
    /// </summary>
    /// <param name="inlineKeys">Inline key values.</param>
    /// <param name="keyFiles">Key-pair files whose public key is trusted.</param>
    /// <returns>The key id → public key map.</returns>
    /// <exception cref="ModulePackageException">A value is malformed.</exception>
    public static IReadOnlyDictionary<string, string> BuildTrustedKeys(
        IEnumerable<string> inlineKeys, IEnumerable<string> keyFiles)
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var value in inlineKeys)
        {
            var separator = value.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || separator == value.Length - 1)
            {
                throw new ModulePackageException(
                    $"'{value}' is not a trusted key; expected 'keyId=base64publickey'.");
            }

            keys[value[..separator]] = value[(separator + 1)..];
        }

        foreach (var path in keyFiles)
        {
            var keyPair = Ed25519Signature.ReadKeyPair(path);
            keys[keyPair.KeyId] = keyPair.PublicKey;
        }

        return keys;
    }
}
