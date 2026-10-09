using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Infrastructure;

/// <summary>
/// Tests for the raw command-line pre-scan: it must resolve <c>--config-dir</c> and the command name
/// before the argument parser (and the sudo re-exec) run.
/// </summary>
[TestClass]
public class CliArgumentsTests
{
    [TestMethod]
    public void TryGetConfigDirArgument_SpaceForm_IsFound()
    {
        Assert.IsTrue(CliArguments.TryGetConfigDirArgument(["--config-dir", "/tmp/x", "backup"], out var configDir));
        Assert.AreEqual("/tmp/x", configDir);
    }

    [TestMethod]
    public void TryGetConfigDirArgument_EqualsForm_IsFound()
    {
        Assert.IsTrue(CliArguments.TryGetConfigDirArgument(["--config-dir=/tmp/x", "backup"], out var configDir));
        Assert.AreEqual("/tmp/x", configDir);
    }

    [TestMethod]
    public void TryGetConfigDirArgument_AfterTheSubcommand_IsFound()
    {
        Assert.IsTrue(CliArguments.TryGetConfigDirArgument(["backup", "--config-dir", "/tmp/x"], out var configDir));
        Assert.AreEqual("/tmp/x", configDir);
    }

    [TestMethod]
    public void TryGetConfigDirArgument_Absent_ReturnsFalse()
    {
        Assert.IsFalse(CliArguments.TryGetConfigDirArgument(["backup", "manifest"], out var configDir));
        Assert.AreEqual(string.Empty, configDir);
    }

    [TestMethod]
    public void TryGetConfigDirArgument_MissingValue_ReturnsFalse()
    {
        Assert.IsFalse(CliArguments.TryGetConfigDirArgument(["backup", "--config-dir"], out _));
        Assert.IsFalse(CliArguments.TryGetConfigDirArgument(["--config-dir", "--no-manifest"], out _));
    }

    [TestMethod]
    public void TryGetConfigDirArgument_EmptyValue_ReturnsFalse()
    {
        Assert.IsFalse(CliArguments.TryGetConfigDirArgument(["--config-dir="], out _));
        Assert.IsFalse(CliArguments.TryGetConfigDirArgument(["--config-dir=   "], out _));
    }

    [TestMethod]
    public void FindCommandName_SkipsTheConfigDirOptionAndItsValue()
    {
        Assert.AreEqual("status", CliArguments.FindCommandName(["--config-dir", "/tmp/x", "status"]));
        Assert.AreEqual("backup", CliArguments.FindCommandName(["--config-dir=/tmp/x", "backup", "manifest"]));
    }

    [TestMethod]
    public void FindCommandName_PlainCommand_IsReturned()
    {
        Assert.AreEqual("backup", CliArguments.FindCommandName(["backup"]));
    }

    [TestMethod]
    public void FindCommandName_OptionsOnly_ReturnsNull()
    {
        Assert.IsNull(CliArguments.FindCommandName(["--help"]));
        Assert.IsNull(CliArguments.FindCommandName([]));
    }

    [TestMethod]
    public void RemoveConfigDirArgument_SpaceForm_IsRemovedWithItsValue()
    {
        CollectionAssert.AreEqual(
            new[] { "backup", "manifest" },
            CliArguments.RemoveConfigDirArgument(["--config-dir", "/tmp/x", "backup", "manifest"]));
    }

    [TestMethod]
    public void RemoveConfigDirArgument_AfterTheSubcommand_IsRemoved()
    {
        CollectionAssert.AreEqual(
            new[] { "status" },
            CliArguments.RemoveConfigDirArgument(["status", "--config-dir", "/tmp/x"]));
    }

    [TestMethod]
    public void RemoveConfigDirArgument_EqualsForm_IsRemoved()
    {
        CollectionAssert.AreEqual(
            new[] { "backup", "manifest" },
            CliArguments.RemoveConfigDirArgument(["backup", "--config-dir=/tmp/x", "manifest"]));
    }

    [TestMethod]
    public void RemoveConfigDirArgument_Absent_LeavesArgumentsUntouched()
    {
        CollectionAssert.AreEqual(
            new[] { "backup", "--output", "/tmp/x.zip" },
            CliArguments.RemoveConfigDirArgument(["backup", "--output", "/tmp/x.zip"]));
    }

    [TestMethod]
    public void RemoveConfigDirArgument_TrailingOptionWithoutValueIsKept()
    {
        CollectionAssert.AreEqual(
            new[] { "backup", "--config-dir" },
            CliArguments.RemoveConfigDirArgument(["backup", "--config-dir"]));
    }

    [TestMethod]
    public void IsHelpOrVersionRequest_TrailingHelpForARootCommand_IsDetected()
    {
        Assert.IsTrue(CliArguments.IsHelpOrVersionRequest(["backup", "--help"]));
        Assert.IsTrue(CliArguments.IsHelpOrVersionRequest(["backup", "manifest", "-h"]));
        Assert.IsTrue(CliArguments.IsHelpOrVersionRequest(["--version"]));
    }

    [TestMethod]
    public void IsHelpOrVersionRequest_RealCommand_ReturnsFalse()
    {
        Assert.IsFalse(CliArguments.IsHelpOrVersionRequest(["backup"]));
        Assert.IsFalse(CliArguments.IsHelpOrVersionRequest(["backup", "--output", "/tmp/x.zip"]));
        Assert.IsFalse(CliArguments.IsHelpOrVersionRequest(["status"]));
    }
}
