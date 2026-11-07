using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Supervisor;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// Tests for <see cref="ModuleRuntimeArtifacts"/>: runtime directory creation, stale socket removal
/// and owner-only socket permissions.
/// </summary>
[TestClass]
public class ModuleRuntimeArtifactsTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "dnc-artifacts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A test may have left a socket behind on a platform that ignores the delete.
        }
    }

    [TestMethod]
    public void Prepare_UnixSocketEndpoint_CreatesParentAndRemovesStaleSocket()
    {
        var socketPath = Path.Combine(_root, "run", "module.sock");
        Directory.CreateDirectory(Path.GetDirectoryName(socketPath)!);
        File.WriteAllText(socketPath, "stale");

        var prepared = ModuleRuntimeArtifacts.Prepare(ModuleEndpoint.UnixDomainSocket(socketPath), runDirectory: null);

        Assert.IsTrue(prepared);
        Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(socketPath)));
        Assert.IsFalse(File.Exists(socketPath), "A stale socket file must be removed so the host can re-bind.");
    }

    [TestMethod]
    public void Prepare_RunDirectory_CreatesItOwnerOnly()
    {
        var runDirectory = Path.Combine(_root, "modules", "x", "run");

        ModuleRuntimeArtifacts.Prepare(ModuleEndpoint.UnixDomainSocket(Path.Combine(runDirectory, "module.sock")), runDirectory);

        Assert.IsTrue(Directory.Exists(runDirectory));

        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(runDirectory);
            Assert.AreEqual(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                mode);
        }
    }

    [TestMethod]
    public void RestrictSocket_AppliesOwnerOnlyMode()
    {
        var socketPath = Path.Combine(_root, "module.sock");
        File.WriteAllText(socketPath, "socket");
        File.SetUnixFileMode(
            socketPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        var restricted = ModuleRuntimeArtifacts.RestrictSocket(ModuleEndpoint.UnixDomainSocket(socketPath));

        Assert.IsTrue(restricted);

        if (!OperatingSystem.IsWindows())
        {
            Assert.AreEqual(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(socketPath));
        }
    }

    [TestMethod]
    public void ExpectedSocketMode_IsOwnerReadWrite()
    {
        var mode = ModuleRuntimeArtifacts.ExpectedSocketMode(
            ModuleEndpoint.UnixDomainSocket(Path.Combine(_root, "module.sock")));

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [TestMethod]
    public void ExpectedSocketMode_ForTcpEndpoint_IsNull()
    {
        Assert.IsNull(ModuleRuntimeArtifacts.ExpectedSocketMode(ModuleEndpoint.Tcp(50105)));
    }

    [TestMethod]
    public void Remove_DeletesSocketFile()
    {
        var socketPath = Path.Combine(_root, "module.sock");
        File.WriteAllText(socketPath, "socket");

        ModuleRuntimeArtifacts.Remove(ModuleEndpoint.UnixDomainSocket(socketPath));

        Assert.IsFalse(File.Exists(socketPath));
    }

    [TestMethod]
    public void Remove_MissingSocket_DoesNotThrow()
    {
        ModuleRuntimeArtifacts.Remove(ModuleEndpoint.UnixDomainSocket(Path.Combine(_root, "absent.sock")));
    }

    [TestMethod]
    public void Prepare_RelativeSocketPath_IsRejected()
    {
        var prepared = ModuleRuntimeArtifacts.Prepare(ModuleEndpoint.UnixDomainSocket("relative.sock"), runDirectory: null);

        Assert.IsFalse(prepared);
    }

    [TestMethod]
    public void Prepare_NamedPipeEndpoint_NeedsNoArtifact()
    {
        Assert.IsTrue(ModuleRuntimeArtifacts.Prepare(ModuleEndpoint.NamedPipe("dotnetcloud-x"), runDirectory: null));
    }

    [TestMethod]
    public void Prepare_TcpEndpoint_NeedsNoArtifact()
    {
        Assert.IsTrue(ModuleRuntimeArtifacts.Prepare(ModuleEndpoint.Tcp(50105), runDirectory: null));
    }

    [TestMethod]
    public void RestrictSocket_ForNonSocketEndpoint_ReturnsTrue()
    {
        // TCP has no file to restrict; the caller must not treat that as a failure.
        Assert.IsTrue(ModuleRuntimeArtifacts.RestrictSocket(ModuleEndpoint.Tcp(50105)));
    }
}
