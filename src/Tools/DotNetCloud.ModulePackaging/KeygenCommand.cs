using System.CommandLine;

namespace DotNetCloud.ModulePackaging;

/// <summary>The <c>keygen</c> command.</summary>
internal static class KeygenCommand
{
    /// <summary>Creates the command.</summary>
    /// <returns>The configured command.</returns>
    public static Command Create()
    {
        var keyIdOption = new Option<string>("--key-id")
        {
            Description = "Stable key id verifiers pin (for example 'store-2026' or 'dev-local')",
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

            Program.Info($"key id     : {keyPair.KeyId}");
            Program.Info($"public key : {keyPair.PublicKey}");
            Program.Info($"private key: {path} (keep it secret; never commit it)");
            Program.Info(string.Empty);
            Program.Info("Trust the public key in a verifier with:");
            Program.Info($"  --key {keyPair.KeyId}={keyPair.PublicKey}");

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ModulePackageException)
        {
            Program.Error(ex.Message);
            return 1;
        }
    }
}
