using System.CommandLine;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// Entry point for <c>dotnetcloud-pack</c>: build, sign, verify and inspect `.dncpkg` packages.
/// </summary>
public static class Program
{
    /// <summary>Runs the tool.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A process exit code: 0 on success, 1 on a verification or usage failure.</returns>
    public static int Main(string[] args)
    {
        var rootCommand = new RootCommand(
            "dotnetcloud-pack - build, sign, verify and inspect .dncpkg module packages");

        rootCommand.Subcommands.Add(PackCommand.Create());
        rootCommand.Subcommands.Add(VerifyCommand.Create());
        rootCommand.Subcommands.Add(InspectCommand.Create());
        rootCommand.Subcommands.Add(KeygenCommand.Create());
        rootCommand.Subcommands.Add(FeedCommands.Create());

        return rootCommand.Parse(args).Invoke();
    }

    /// <summary>Writes an error line to standard error.</summary>
    /// <param name="message">The message.</param>
    internal static void Error(string message) => Console.Error.WriteLine($"error: {message}");

    /// <summary>Writes an information line to standard output.</summary>
    /// <param name="message">The message.</param>
    internal static void Info(string message) => Console.Out.WriteLine(message);

    /// <summary>Writes a warning line to standard error.</summary>
    /// <param name="message">The message.</param>
    internal static void Warn(string message) => Console.Error.WriteLine($"warning: {message}");

    /// <summary>Prints structured issues and warnings for a verification result.</summary>
    /// <param name="verification">The verification to report.</param>
    internal static void ReportIssues(ModulePackageVerification verification)
    {
        foreach (var warning in verification.Warnings)
        {
            Warn(warning.ToString());
        }

        foreach (var issue in verification.Issues)
        {
            Error(issue.ToString());
        }
    }

    /// <summary>
    /// Builds the trusted-key map from <c>--key id=base64</c> values and <c>--key-file</c> paths.
    /// </summary>
    /// <param name="inlineKeys">Inline key values.</param>
    /// <param name="keyFiles">Key-pair files whose public key is trusted.</param>
    /// <returns>The key id → public key map.</returns>
    /// <exception cref="ModulePackageException">A value is malformed.</exception>
    internal static IReadOnlyDictionary<string, string> BuildTrustedKeys(
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
