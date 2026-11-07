using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Supervisor;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// Tests for <see cref="ModuleTransportSelection"/>: the single place the core↔module transport is
/// decided.
/// </summary>
/// <remarks>
/// <see cref="ProcessSupervisorOptions.PreferTcpTransport"/> used to be hard-coded to
/// <see langword="true"/> in two call sites and never read, so socket transport could not be
/// enabled at all. These tests pin both the new default and the fact that the legacy flag is now
/// genuinely honoured.
/// </remarks>
[TestClass]
public class ModuleTransportSelectionTests
{
    [TestMethod]
    public void Resolve_AutoOnUnix_UsesUnixDomainSocket()
    {
        var selection = ModuleTransportSelection.Resolve(new ModuleTransportOptions(), new ProcessSupervisorOptions(), isWindows: false);

        Assert.AreEqual(ModuleTransportKind.UnixDomainSocket, selection.Kind);
        Assert.IsTrue(selection.IsSocketTransport);
    }

    [TestMethod]
    public void Resolve_AutoOnWindows_UsesNamedPipe()
    {
        var selection = ModuleTransportSelection.Resolve(new ModuleTransportOptions(), new ProcessSupervisorOptions(), isWindows: true);

        Assert.AreEqual(ModuleTransportKind.NamedPipe, selection.Kind);
    }

    [TestMethod]
    public void Resolve_SocketsModeOnWindows_UsesNamedPipe()
    {
        var transport = new ModuleTransportOptions { Transport = ModuleTransportMode.Sockets };

        var selection = ModuleTransportSelection.Resolve(transport, new ProcessSupervisorOptions(), isWindows: true);

        Assert.AreEqual(ModuleTransportKind.NamedPipe, selection.Kind);
    }

    [TestMethod]
    public void Resolve_TcpMode_UsesTcp()
    {
        var transport = new ModuleTransportOptions { Transport = ModuleTransportMode.Tcp };

        var selection = ModuleTransportSelection.Resolve(transport, new ProcessSupervisorOptions(), isWindows: false);

        Assert.AreEqual(ModuleTransportKind.Tcp, selection.Kind);
        Assert.IsFalse(selection.IsSocketTransport);
    }

    [TestMethod]
    public void Resolve_PreferTcpTransportFalse_UsesSockets()
    {
        // Regression: the option used to be forced true, so setting it false had no effect.
        var supervisor = new ProcessSupervisorOptions { PreferTcpTransport = false };

        var selection = ModuleTransportSelection.Resolve(new ModuleTransportOptions(), supervisor, isWindows: false);

        Assert.AreEqual(ModuleTransportKind.UnixDomainSocket, selection.Kind);
    }

    [TestMethod]
    public void Resolve_PreferTcpTransportTrue_UsesTcp()
    {
        // Legacy opt-in still works for deployments that cannot share a socket path.
        var supervisor = new ProcessSupervisorOptions { PreferTcpTransport = true };

        var selection = ModuleTransportSelection.Resolve(new ModuleTransportOptions(), supervisor, isWindows: false);

        Assert.AreEqual(ModuleTransportKind.Tcp, selection.Kind);
    }

    [TestMethod]
    public void Resolve_ExplicitTcpModeWinsOverLegacyFlag()
    {
        var transport = new ModuleTransportOptions { Transport = ModuleTransportMode.Tcp };
        var supervisor = new ProcessSupervisorOptions { PreferTcpTransport = false };

        var selection = ModuleTransportSelection.Resolve(transport, supervisor, isWindows: false);

        Assert.AreEqual(ModuleTransportKind.Tcp, selection.Kind);
    }

    [TestMethod]
    public void Resolve_ExplicitSocketsModeWinsOverLegacyFlag()
    {
        var transport = new ModuleTransportOptions { Transport = ModuleTransportMode.Sockets };
        var supervisor = new ProcessSupervisorOptions { PreferTcpTransport = true };

        var selection = ModuleTransportSelection.Resolve(transport, supervisor, isWindows: false);

        Assert.AreEqual(ModuleTransportKind.UnixDomainSocket, selection.Kind);
    }

    [TestMethod]
    public void Resolve_BlankPipePrefix_FallsBackToDefaultPrefix()
    {
        var transport = new ModuleTransportOptions { NamedPipePrefix = "  " };

        var selection = ModuleTransportSelection.Resolve(transport, new ProcessSupervisorOptions(), isWindows: true);

        Assert.AreEqual("dotnetcloud", selection.NamedPipePrefix);
    }

    [TestMethod]
    public void Resolve_BlankSocketDirectory_TreatedAsUnset()
    {
        var transport = new ModuleTransportOptions { UnixSocketDirectory = "  " };

        var selection = ModuleTransportSelection.Resolve(transport, new ProcessSupervisorOptions(), isWindows: false);

        Assert.IsNull(selection.UnixSocketDirectory);
    }

    [TestMethod]
    public void DefaultPreferTcpTransport_IsFalse()
    {
        Assert.IsFalse(new ProcessSupervisorOptions().PreferTcpTransport);
    }

    [TestMethod]
    public void DefaultTransportMode_IsAuto()
    {
        Assert.AreEqual(ModuleTransportMode.Auto, new ModuleTransportOptions().Transport);
    }
}
