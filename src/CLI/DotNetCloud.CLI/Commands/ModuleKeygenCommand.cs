using System.CommandLine;
using DotNetCloud.ModulePackaging;

namespace DotNetCloud.CLI.Commands;

/// <summary>The <c>module keygen</c> command.</summary>
internal static class ModuleKeygenCommand
{
    /// <summary>Creates the command.</summary>
    /// <returns>The configured command.</returns>
    public static Command Create()
    {
        var keyIdOption = new Option<string>("--key-id")
        {
            Description = "Stable key id verifiers pin (for example 'store-2027' or 'dev-local')",
            Required = true,
        };

        var outputOption = new Option<string?>("--output")
        {
            Description = "Key-pair file (default: ./<key-id>.key.json)",
        };

        var command = new Command("keygen", "Generate an Ed25519 signing key pair")
        {
            keyIdOption,
            outputOption,
        };

        command.SetAction(parseResult => Run(
            parseResult.GetValue(keyIdOption)!,
            parseResult.GetValue(outputOption)));

        return command;
    }

    private static int Run(string keyId, string? output)
    {
        try
        {
            var keyPair = Ed25519Signature.GenerateKeyPair(keyId);
            var path = Path.GetFullPath(output ?? $"{keyId}.key.json");

            Ed25519Signature.WriteKeyPair(keyPair, path);

            ModulePackageConsole.Info($"key id     : {keyPair.KeyId}");
            ModulePackageConsole.Info($"public key : {keyPair.PublicKey}");
            ModulePackageConsole.Info($"private key: {path} (keep it secret; never commit it)");
            ModulePackageConsole.Info(string.Empty);
            ModulePackageConsole.Info("Trust the public key in a verifier with:");
            ModulePackageConsole.Info($"  --key {keyPair.KeyId}={keyPair.PublicKey}");

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ModulePackageException)
        {
            ModulePackageConsole.Error(ex.Message);
            return 1;
        }
    }
}
