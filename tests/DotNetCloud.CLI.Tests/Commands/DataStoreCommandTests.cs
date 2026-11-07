using DotNetCloud.CLI.Commands;

namespace DotNetCloud.CLI.Tests.Commands;

/// <summary>
/// Structure tests for the <c>dotnetcloud datastore</c> command group.
/// </summary>
[TestClass]
public class DataStoreCommandTests
{
    [TestMethod]
    public void DataStoreCommand_HasAllLifecycleSubcommands()
    {
        var command = DataStoreCommands.Create();

        Assert.AreEqual("datastore", command.Name);

        var names = command.Subcommands.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        CollectionAssert.AreEquivalent(
            new[] { "destroy", "preflight", "provision", "reset-credentials", "rotate", "status" },
            names);
    }

    [TestMethod]
    public void DataStoreStatusCommand_HasOptionalModuleArgument()
    {
        var status = DataStoreCommands.Create().Subcommands.First(c => c.Name == "status");

        Assert.AreEqual(1, status.Arguments.Count);
        Assert.AreEqual("module", status.Arguments[0].Name);
        Assert.AreEqual(0, status.Arguments[0].Arity.MinimumNumberOfValues);
    }

    [TestMethod]
    public void DataStoreProvisionCommand_HasModuleArgumentAndConnectionLimitOption()
    {
        var provision = DataStoreCommands.Create().Subcommands.First(c => c.Name == "provision");

        Assert.AreEqual(1, provision.Arguments.Count);
        Assert.AreEqual("module", provision.Arguments[0].Name);
        Assert.AreEqual(1, provision.Arguments[0].Arity.MinimumNumberOfValues);
        Assert.IsTrue(provision.Options.Any(o => o.Name == "--connection-limit"),
            "Expected --connection-limit option");
    }

    [TestMethod]
    public void DataStoreRotateCommand_HasModuleArgumentAndConnectionLimitOption()
    {
        var rotate = DataStoreCommands.Create().Subcommands.First(c => c.Name == "rotate");

        Assert.AreEqual("module", rotate.Arguments[0].Name);
        Assert.IsTrue(rotate.Options.Any(o => o.Name == "--connection-limit"),
            "Expected --connection-limit option");
    }

    [TestMethod]
    public void DataStoreResetCredentialsCommand_HasModuleArgument()
    {
        var reset = DataStoreCommands.Create().Subcommands.First(c => c.Name == "reset-credentials");

        Assert.AreEqual("module", reset.Arguments[0].Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(reset.Description));
    }

    [TestMethod]
    public void DataStoreDestroyCommand_HasModuleArgumentAndYesOption()
    {
        var destroy = DataStoreCommands.Create().Subcommands.First(c => c.Name == "destroy");

        Assert.AreEqual("module", destroy.Arguments[0].Name);
        Assert.IsTrue(destroy.Options.Any(o => o.Name == "--yes"),
            "Expected --yes option for non-interactive use");
    }
}
