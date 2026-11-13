using System.CommandLine;

namespace DotNetCloud.ModulePackaging;

/// <summary>The <c>verify</c> command.</summary>
internal static class VerifyCommand
{
    /// <summary>Creates the command.</summary>
    /// <returns>The configured command.</returns>
    public static Command Create()
    {
        var packageArgument = new Argument<string>("package")
        {
            Description = "The .dncpkg file to verify",
        };

        var signatureOption = new Option<string?>("--signature")
        {
            Description = "Detached signature file (default: <package>.sig)",
        };

        var keyOption = new Option<string[]>("--key")
        {
            Description = "Trusted public key as 'keyId=base64' (repeatable)",
            AllowMultipleArgumentsPerToken = true,
        };

        var keyFileOption = new Option<string[]>("--key-file")
        {
            Description = "Key-pair file whose public key is trusted (repeatable)",
            AllowMultipleArgumentsPerToken = true,
        };

        var allowUnsignedOption = new Option<bool>("--allow-unverified")
        {
            Description = "Accept an unsigned package (sideload only)",
        };

        var command = new Command("verify", "Verify a package's structure, manifest and signature")
        {
            packageArgument,
            signatureOption,
            keyOption,
            keyFileOption,
            allowUnsignedOption,
        };

        command.SetAction(parseResult => RunAsync(
            parseResult.GetValue(packageArgument)!,
            parseResult.GetValue(signatureOption),
            parseResult.GetValue(keyOption) ?? [],
            parseResult.GetValue(keyFileOption) ?? [],
            parseResult.GetValue(allowUnsignedOption)));

        return command;
    }

    private static async Task<int> RunAsync(
        string package,
        string? signature,
        string[] inlineKeys,
        string[] keyFiles,
        bool allowUnsigned)
    {
        try
        {
            var options = new ModulePackageVerificationOptions
            {
                TrustedKeys = Program.BuildTrustedKeys(inlineKeys, keyFiles),
                AllowUnsigned = allowUnsigned,
            };

            var verification = await ModulePackageVerifier.VerifyAsync(
                package, signature, options, CancellationToken.None);

            Program.ReportIssues(verification);

            Program.Info($"package : {Path.GetFileName(package)}");
            Program.Info($"id      : {verification.ModuleId ?? "(unnamed)"}");
            Program.Info($"version : {verification.Version ?? "(unknown)"}");
            Program.Info($"entries : {verification.EntryCount} ({verification.UncompressedBytes} bytes uncompressed)");
            Program.Info($"sha256  : {verification.Sha256 ?? "(unreadable)"}");
            Program.Info($"key     : {verification.KeyId ?? "(unsigned)"}");
            Program.Info(verification.IsValid ? "OK" : "FAILED");

            return verification.IsValid ? 0 : 1;
        }
        catch (ModulePackageException ex)
        {
            Program.Error(ex.Message);
            foreach (var issue in ex.Issues)
            {
                Program.Error($"  {issue}");
            }

            return 1;
        }
    }
}
