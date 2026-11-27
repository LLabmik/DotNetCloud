using System.CommandLine;
using DotNetCloud.CLI.Commands;
using DotNetCloud.ModulePackaging;

namespace DotNetCloud.CLI.Tests;

/// <summary>
/// Command-surface tests for the package verbs that used to live in <c>dotnetcloud-pack</c>:
/// <c>module pack</c>, <c>module inspect</c>, <c>module keygen</c>, <c>module feed</c> and the
/// options added to <c>module verify</c>.
/// </summary>
/// <remarks>
/// The format itself is covered by <c>DotNetCloud.Core.Packaging.Tests</c>. These tests prove the
/// verbs are reachable on the <c>module</c> command and that packing through the CLI produces a
/// package the verifier accepts.
/// </remarks>
[TestClass]
public class ModulePackageCommandTests
{
    private const string TestManifest = """
        {
          "manifestVersion": 2,
          "id": "dotnetcloud.cli-test",
          "name": "CLI Test",
          "version": "1.0.0",
          "description": "Fixture module for the CLI package verbs.",
          "publisher": { "id": "dotnetcloud", "name": "DotNetCloud" },
          "moduleApiVersion": 1,
          "minCoreVersion": "0.7.0",
          "dataStore": "none",
          "capabilities": [],
          "userConsentScopes": [],
          "events": { "publishes": [], "subscribes": [] }
        }
        """;

    [TestMethod]
    public void VerifyCommand_AcceptsThePackerOptions()
    {
        var verify = ModuleCommands.Create().Subcommands.Single(subcommand => subcommand.Name == "verify");
        var options = verify.Options.Select(option => option.Name).ToList();

        CollectionAssert.IsSubsetOf(
            new[] { "--signature", "--key", "--key-file", "--allow-unverified" }, options);
    }

    [TestMethod]
    public void Pack_ThroughTheCli_ProducesAPackageTheVerifierAccepts()
    {
        var root = CreateRoot();
        var workspace = Path.Combine(Path.GetTempPath(), "dnc-cli-pack-" + Guid.CreateVersion7().ToString("N"));
        var staging = Path.Combine(workspace, "staging");
        Directory.CreateDirectory(Path.Combine(staging, "files", "linux-x64"));

        try
        {
            File.WriteAllText(Path.Combine(staging, "manifest.json"), TestManifest);
            File.WriteAllText(Path.Combine(staging, "files", "linux-x64", "dotnetcloud.cli-test.dll"), "fixture");
            File.WriteAllText(Path.Combine(staging, "LICENSE"), "AGPL-3.0-or-later");
            File.WriteAllText(Path.Combine(staging, "README.md"), "# CLI Test");
            File.WriteAllText(Path.Combine(staging, "CHANGELOG.md"), "## 1.0.0");

            var keyPath = Path.Combine(workspace, "test.key.json");
            var keyPair = Ed25519Signature.GenerateKeyPair("cli-test-key");
            Ed25519Signature.WriteKeyPair(keyPair, keyPath);

            var packagePath = Path.Combine(workspace, "dotnetcloud.cli-test-1.0.0.dncpkg");

            Assert.AreEqual(0, root.Parse([
                "module", "pack",
                "--input", staging,
                "--output", packagePath,
                "--sign-key", keyPath,
            ]).Invoke(), "module pack failed");

            Assert.IsTrue(File.Exists(packagePath), packagePath);
            Assert.IsTrue(File.Exists(ModulePackageFormat.GetSignaturePath(packagePath)), "no signature written");

            var verification = ModulePackageVerifier.VerifyAsync(
                packagePath,
                signaturePath: null,
                options: new ModulePackageVerificationOptions
                {
                    TrustedKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [keyPair.KeyId] = keyPair.PublicKey,
                    },
                }).GetAwaiter().GetResult();

            Assert.IsTrue(verification.IsValid, string.Join("; ", verification.Issues.Select(issue => issue.ToString())));
            Assert.AreEqual("dotnetcloud.cli-test", verification.ModuleId);
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    private static RootCommand CreateRoot()
    {
        var root = new RootCommand("test root");
        root.Subcommands.Add(ModuleCommands.Create());
        return root;
    }
}
