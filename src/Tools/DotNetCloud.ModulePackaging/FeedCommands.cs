using System.CommandLine;

namespace DotNetCloud.ModulePackaging;

/// <summary>The <c>feed</c> command group (local package feeds).</summary>
internal static class FeedCommands
{
    /// <summary>Creates the <c>feed</c> command.</summary>
    /// <returns>The configured command.</returns>
    public static Command Create()
    {
        var command = new Command("feed", "Work with a local package feed (index.json + packages)");
        command.Subcommands.Add(CreateList());
        command.Subcommands.Add(CreateVerify());
        return command;
    }

    private static Command CreateList()
    {
        var feedOption = new Option<string>("--feed")
        {
            Description = "Feed directory",
            Required = true,
        };

        var command = new Command("list", "List the packages in a feed") { feedOption };

        command.SetAction(parseResult => ListAsync(parseResult.GetValue(feedOption)!));
        return command;
    }

    private static Command CreateVerify()
    {
        var feedOption = new Option<string>("--feed")
        {
            Description = "Feed directory",
            Required = true,
        };

        var idOption = new Option<string>("--id")
        {
            Description = "Module id",
            Required = true,
        };

        var versionOption = new Option<string>("--version")
        {
            Description = "Module version",
            Required = true,
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

        var command = new Command("verify", "Verify one package listed in a feed")
        {
            feedOption,
            idOption,
            versionOption,
            keyOption,
            keyFileOption,
            allowUnsignedOption,
        };

        command.SetAction(parseResult => VerifyAsync(
            parseResult.GetValue(feedOption)!,
            parseResult.GetValue(idOption)!,
            parseResult.GetValue(versionOption)!,
            parseResult.GetValue(keyOption) ?? [],
            parseResult.GetValue(keyFileOption) ?? [],
            parseResult.GetValue(allowUnsignedOption)));

        return command;
    }

    private static async Task<int> ListAsync(string feedDirectory)
    {
        try
        {
            var index = await ModuleFeed.ReadAsync(feedDirectory);

            if (index.Packages.Count == 0)
            {
                Program.Info($"No packages in '{Path.GetFullPath(feedDirectory)}'.");
                return 0;
            }

            Program.Info($"schema {index.SchemaVersion}, generated {index.GeneratedAtUtc:u}");
            Program.Info(string.Empty);

            foreach (var entry in index.Packages)
            {
                Program.Info($"{entry.Id} {entry.Version}  {entry.File}  {entry.Size} bytes  {entry.Sha256[..16]}...  key={entry.KeyId ?? "(none)"}");
            }

            return 0;
        }
        catch (ModulePackageException ex)
        {
            Program.Error(ex.Message);
            return 1;
        }
    }

    private static async Task<int> VerifyAsync(
        string feedDirectory,
        string moduleId,
        string version,
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

            var verification = await ModuleFeed.VerifyEntryAsync(
                feedDirectory, moduleId, version, options, CancellationToken.None);

            Program.ReportIssues(verification);
            Program.Info(verification.IsValid
                ? $"{moduleId} {version}: OK"
                : $"{moduleId} {version}: FAILED");

            return verification.IsValid ? 0 : 1;
        }
        catch (ModulePackageException ex)
        {
            Program.Error(ex.Message);
            return 1;
        }
    }
}
