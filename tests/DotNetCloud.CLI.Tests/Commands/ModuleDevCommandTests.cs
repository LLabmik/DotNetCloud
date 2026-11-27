using System.CommandLine;
using DotNetCloud.CLI.Commands;

namespace DotNetCloud.CLI.Tests.Commands;

/// <summary>
/// The refusals and the argument surface of <c>dotnetcloud module dev</c>.
/// </summary>
/// <remarks>
/// What a deploy does to a module directory is covered by
/// <c>ModuleDevDeployServiceTests</c> in Core.Server; this class covers the checks that must stop the
/// command before it touches anything, which is the part a wrong binary would cost a developer their
/// running platform.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ModuleDevCommandTests
{
    private const string ModulesDirectoryEnvironmentVariable = "DOTNETCLOUD_MODULES_DIR";
    private const string EnvironmentVariable = "ASPNETCORE_ENVIRONMENT";

    private string? _previousEnvironment;
    private string? _previousModulesDirectory;

    [TestInitialize]
    public void Initialize()
    {
        _previousEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        _previousModulesDirectory = Environment.GetEnvironmentVariable(ModulesDirectoryEnvironmentVariable);
        Environment.SetEnvironmentVariable(ModulesDirectoryEnvironmentVariable, null);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariable, _previousEnvironment);
        Environment.SetEnvironmentVariable(ModulesDirectoryEnvironmentVariable, _previousModulesDirectory);
    }

    [TestMethod]
    public void DevCommand_HasTheDocumentedSurface()
    {
        var dev = ModuleCommands.Create().Subcommands.Single(subcommand => subcommand.Name == "dev");

        Assert.AreEqual(1, dev.Arguments.Count);
        Assert.AreEqual("path", dev.Arguments[0].Name);

        var options = dev.Options.Select(option => option.Name).ToList();
        CollectionAssert.IsSubsetOf(
            new[] { "--watch", "--configuration", "--no-publish", "--allow-schema-change", "--restore" },
            options);
    }

    [TestMethod]
    public void Dev_OutsideDevelopment_IsRefusedBeforeTouchingAnything()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariable, "Production");

        var exitCode = Run("dev", Path.Combine(Path.GetTempPath(), "some-module"));

        Assert.AreEqual(1, exitCode);
    }

    [TestMethod]
    public void Dev_WithoutAnAbsoluteModulesDirectory_IsRefused()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariable, "Development");
        Environment.SetEnvironmentVariable(ModulesDirectoryEnvironmentVariable, "modules");

        var exitCode = Run("dev", Path.Combine(Path.GetTempPath(), "some-module"));

        Assert.AreEqual(1, exitCode);
    }

    [TestMethod]
    public void Dev_WithAMissingPath_IsRefused()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariable, "Development");
        Environment.SetEnvironmentVariable(ModulesDirectoryEnvironmentVariable, Path.GetTempPath());

        var exitCode = Run("dev", Path.Combine(Path.GetTempPath(), "definitely-not-a-module-" + Guid.CreateVersion7().ToString("N")));

        Assert.AreEqual(1, exitCode);
    }

    private static int Run(params string[] args)
    {
        var root = new RootCommand("test root");
        root.Subcommands.Add(ModuleCommands.Create());
        return root.Parse(args).Invoke();
    }
}
