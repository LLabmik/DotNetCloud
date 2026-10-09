using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Infrastructure;

/// <summary>
/// Tests for the sudo re-exec argument construction. <c>sudo</c> resets the environment, so the
/// configuration directory the caller resolved has to travel on the command line instead.
/// </summary>
[TestClass]
public class SudoHelperTests
{
    private const string Exe = "/opt/dotnetcloud/dotnetcloud";

    [TestMethod]
    public void BuildSudoArguments_InsertsTheResolvedConfigDirectory()
    {
        var arguments = SudoHelper.BuildSudoArguments(Exe, "/tmp/custom-config", ["backup", "manifest"]);

        CollectionAssert.AreEqual(
            new[] { Exe, "--config-dir", "/tmp/custom-config", "backup", "manifest" },
            arguments.ToArray());
    }

    [TestMethod]
    public void BuildSudoArguments_PreservesTheOriginalArguments()
    {
        var arguments = SudoHelper.BuildSudoArguments(
            Exe, "/etc/dotnetcloud", ["backup", "--output", "/tmp/x.zip", "--no-manifest"]);

        CollectionAssert.AreEqual(
            new[] { Exe, "--config-dir", "/etc/dotnetcloud", "backup", "--output", "/tmp/x.zip", "--no-manifest" },
            arguments.ToArray());
    }

    [TestMethod]
    public void BuildSudoArguments_ExplicitConfigDirIsNotDuplicated()
    {
        var arguments = SudoHelper.BuildSudoArguments(
            Exe, "/tmp/resolved", ["--config-dir", "/tmp/explicit", "backup"]);

        CollectionAssert.AreEqual(
            new[] { Exe, "--config-dir", "/tmp/explicit", "backup" },
            arguments.ToArray());
    }

    [TestMethod]
    public void BuildSudoArguments_ExplicitConfigDirEqualsFormIsNotDuplicated()
    {
        var arguments = SudoHelper.BuildSudoArguments(
            Exe, "/tmp/resolved", ["--config-dir=/tmp/explicit", "backup"]);

        CollectionAssert.AreEqual(
            new[] { Exe, "--config-dir=/tmp/explicit", "backup" },
            arguments.ToArray());
    }

    [TestMethod]
    public void BuildSudoArguments_BlankConfigDirectory_IsOmitted()
    {
        var arguments = SudoHelper.BuildSudoArguments(Exe, "   ", ["status"]);

        CollectionAssert.AreEqual(new[] { Exe, "status" }, arguments.ToArray());
    }

    [TestMethod]
    public void BuildSudoArguments_EntryAssembly_IsPlacedAfterTheExecutable()
    {
        var arguments = SudoHelper.BuildSudoArguments(
            "/usr/lib/dotnet/dotnet",
            "/tmp/custom-config",
            ["backup"],
            "/opt/dotnetcloud/dotnetcloud.dll");

        CollectionAssert.AreEqual(
            new[]
            {
                "/usr/lib/dotnet/dotnet", "/opt/dotnetcloud/dotnetcloud.dll", "--config-dir", "/tmp/custom-config", "backup"
            },
            arguments.ToArray());
    }

    [TestMethod]
    public void GetEntryAssemblyArgument_ApphostLaunch_ReturnsNull()
    {
        Assert.IsNull(SudoHelper.GetEntryAssemblyArgument("/opt/dotnetcloud/dotnetcloud"));
        Assert.IsNull(SudoHelper.GetEntryAssemblyArgument("/usr/local/bin/dotnetcloud"));
    }

    [TestMethod]
    public void GetEntryAssemblyArgument_MuxerLaunch_ReturnsTheAbsoluteAssemblyPath()
    {
        // The test host itself runs as `dotnet .../DotNetCloud.CLI.Tests.dll`, so the muxer branch is
        // exercised for real here.
        var entryAssembly = SudoHelper.GetEntryAssemblyArgument("/usr/lib/dotnet/dotnet");

        Assert.IsNotNull(entryAssembly);
        Assert.IsTrue(Path.IsPathRooted(entryAssembly));
        Assert.IsTrue(entryAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
    }
}
