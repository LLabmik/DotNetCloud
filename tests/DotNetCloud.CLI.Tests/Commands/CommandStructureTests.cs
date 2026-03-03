using System.CommandLine;
using DotNetCloud.CLI.Commands;

namespace DotNetCloud.CLI.Tests.Commands;

[TestClass]
public class CommandStructureTests
{
    [TestMethod]
    public void RootCommand_ContainsAllExpectedSubcommands()
    {
        var root = new RootCommand("test");
        root.Subcommands.Add(SetupCommand.Create());
        root.Subcommands.Add(ServiceCommands.CreateServe());
        root.Subcommands.Add(ServiceCommands.CreateStop());
        root.Subcommands.Add(ServiceCommands.CreateStatus());
        root.Subcommands.Add(ServiceCommands.CreateRestart());
        root.Subcommands.Add(ModuleCommands.Create());
        root.Subcommands.Add(ComponentCommands.Create());
        root.Subcommands.Add(LogCommands.Create());
        root.Subcommands.Add(BackupCommands.Create());
        root.Subcommands.Add(MiscCommands.CreateUpdate());
        root.Subcommands.Add(MiscCommands.CreateVersion());

        var names = root.Subcommands.Select(c => c.Name).ToList();
        CollectionAssert.Contains(names, "setup");
        CollectionAssert.Contains(names, "serve");
        CollectionAssert.Contains(names, "stop");
        CollectionAssert.Contains(names, "status");
        CollectionAssert.Contains(names, "restart");
        CollectionAssert.Contains(names, "module");
        CollectionAssert.Contains(names, "component");
        CollectionAssert.Contains(names, "logs");
        CollectionAssert.Contains(names, "backup");
        CollectionAssert.Contains(names, "update");
        CollectionAssert.Contains(names, "version");
    }

    [TestMethod]
    public void ServeCommand_HasForegroundOption()
    {
        var command = ServiceCommands.CreateServe();
        Assert.AreEqual("serve", command.Name);

        var option = command.Options.FirstOrDefault(o => o.Name == "--foreground");
        Assert.IsNotNull(option, "Expected --foreground option");
    }

    [TestMethod]
    public void StopCommand_HasCorrectName()
    {
        var command = ServiceCommands.CreateStop();
        Assert.AreEqual("stop", command.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(command.Description));
    }

    [TestMethod]
    public void StatusCommand_HasCorrectName()
    {
        var command = ServiceCommands.CreateStatus();
        Assert.AreEqual("status", command.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(command.Description));
    }

    [TestMethod]
    public void RestartCommand_HasCorrectName()
    {
        var command = ServiceCommands.CreateRestart();
        Assert.AreEqual("restart", command.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(command.Description));
    }

    [TestMethod]
    public void ModuleCommand_HasAllSubcommands()
    {
        var command = ModuleCommands.Create();
        Assert.AreEqual("module", command.Name);

        var subNames = command.Subcommands.Select(c => c.Name).ToList();
        CollectionAssert.Contains(subNames, "list");
        CollectionAssert.Contains(subNames, "start");
        CollectionAssert.Contains(subNames, "stop");
        CollectionAssert.Contains(subNames, "restart");
        CollectionAssert.Contains(subNames, "install");
        CollectionAssert.Contains(subNames, "uninstall");
        Assert.AreEqual(6, command.Subcommands.Count);
    }

    [TestMethod]
    public void ModuleStartCommand_HasModuleArgument()
    {
        var command = ModuleCommands.Create();
        var startCmd = command.Subcommands.First(c => c.Name == "start");
        Assert.AreEqual(1, startCmd.Arguments.Count);
        Assert.AreEqual("module", startCmd.Arguments[0].Name);
    }

    [TestMethod]
    public void ModuleStopCommand_HasModuleArgument()
    {
        var command = ModuleCommands.Create();
        var stopCmd = command.Subcommands.First(c => c.Name == "stop");
        Assert.AreEqual(1, stopCmd.Arguments.Count);
        Assert.AreEqual("module", stopCmd.Arguments[0].Name);
    }

    [TestMethod]
    public void ModuleInstallCommand_HasModuleArgument()
    {
        var command = ModuleCommands.Create();
        var installCmd = command.Subcommands.First(c => c.Name == "install");
        Assert.AreEqual(1, installCmd.Arguments.Count);
        Assert.AreEqual("module", installCmd.Arguments[0].Name);
    }

    [TestMethod]
    public void ModuleUninstallCommand_HasModuleArgument()
    {
        var command = ModuleCommands.Create();
        var uninstallCmd = command.Subcommands.First(c => c.Name == "uninstall");
        Assert.AreEqual(1, uninstallCmd.Arguments.Count);
        Assert.AreEqual("module", uninstallCmd.Arguments[0].Name);
    }

    [TestMethod]
    public void ComponentCommand_HasStatusAndRestartSubcommands()
    {
        var command = ComponentCommands.Create();
        Assert.AreEqual("component", command.Name);

        var subNames = command.Subcommands.Select(c => c.Name).ToList();
        CollectionAssert.Contains(subNames, "status");
        CollectionAssert.Contains(subNames, "restart");
        Assert.AreEqual(2, command.Subcommands.Count);
    }

    [TestMethod]
    public void ComponentStatusCommand_HasComponentArgument()
    {
        var command = ComponentCommands.Create();
        var statusCmd = command.Subcommands.First(c => c.Name == "status");
        Assert.AreEqual(1, statusCmd.Arguments.Count);
        Assert.AreEqual("component", statusCmd.Arguments[0].Name);
    }

    [TestMethod]
    public void ComponentRestartCommand_HasComponentArgument()
    {
        var command = ComponentCommands.Create();
        var restartCmd = command.Subcommands.First(c => c.Name == "restart");
        Assert.AreEqual(1, restartCmd.Arguments.Count);
        Assert.AreEqual("component", restartCmd.Arguments[0].Name);
    }

    [TestMethod]
    public void LogsCommand_HasModuleArgument()
    {
        var command = LogCommands.Create();
        Assert.AreEqual("logs", command.Name);
        Assert.IsTrue(command.Arguments.Count >= 1);
    }

    [TestMethod]
    public void LogsCommand_HasLevelOption()
    {
        var command = LogCommands.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "--level");
        Assert.IsNotNull(option, "Expected --level option");
    }

    [TestMethod]
    public void LogsCommand_HasTailOption()
    {
        var command = LogCommands.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "--tail");
        Assert.IsNotNull(option, "Expected --tail option");
    }

    [TestMethod]
    public void LogsCommand_HasFollowOption()
    {
        var command = LogCommands.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "--follow");
        Assert.IsNotNull(option, "Expected --follow option");
    }

    [TestMethod]
    public void BackupCommand_HasOutputOption()
    {
        var command = BackupCommands.Create();
        Assert.AreEqual("backup", command.Name);
        var option = command.Options.FirstOrDefault(o => o.Name == "--output");
        Assert.IsNotNull(option, "Expected --output option");
    }

    [TestMethod]
    public void BackupCommand_HasRestoreAndScheduleSubcommands()
    {
        var command = BackupCommands.Create();
        var subNames = command.Subcommands.Select(c => c.Name).ToList();
        CollectionAssert.Contains(subNames, "restore");
        CollectionAssert.Contains(subNames, "schedule");
        Assert.AreEqual(2, command.Subcommands.Count);
    }

    [TestMethod]
    public void BackupRestoreCommand_HasFileArgument()
    {
        var command = BackupCommands.Create();
        var restoreCmd = command.Subcommands.First(c => c.Name == "restore");
        Assert.AreEqual(1, restoreCmd.Arguments.Count);
        Assert.AreEqual("file", restoreCmd.Arguments[0].Name);
    }

    [TestMethod]
    public void BackupScheduleCommand_HasIntervalOption()
    {
        var command = BackupCommands.Create();
        var scheduleCmd = command.Subcommands.First(c => c.Name == "schedule");
        var option = scheduleCmd.Options.FirstOrDefault(o => o.Name == "--interval");
        Assert.IsNotNull(option, "Expected --interval option");
    }

    [TestMethod]
    public void UpdateCommand_HasCheckOption()
    {
        var command = MiscCommands.CreateUpdate();
        Assert.AreEqual("update", command.Name);
        var option = command.Options.FirstOrDefault(o => o.Name == "--check");
        Assert.IsNotNull(option, "Expected --check option");
    }

    [TestMethod]
    public void VersionCommand_HasCorrectName()
    {
        var command = MiscCommands.CreateVersion();
        Assert.AreEqual("version", command.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(command.Description));
    }

    [TestMethod]
    public void AllCommands_HaveDescriptions()
    {
        var commands = new Command[]
        {
            SetupCommand.Create(),
            ServiceCommands.CreateServe(),
            ServiceCommands.CreateStop(),
            ServiceCommands.CreateStatus(),
            ServiceCommands.CreateRestart(),
            ModuleCommands.Create(),
            ComponentCommands.Create(),
            LogCommands.Create(),
            BackupCommands.Create(),
            MiscCommands.CreateUpdate(),
            MiscCommands.CreateVersion()
        };

        foreach (var cmd in commands)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(cmd.Description),
                $"Command '{cmd.Name}' is missing a description");
        }
    }
}
