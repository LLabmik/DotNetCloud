using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Supervisor;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// Tests for <see cref="ModuleEndpointAllocator"/>: socket path layout, named pipe naming and TCP
/// port probing.
/// </summary>
[TestClass]
public class ModuleEndpointAllocatorTests
{
    private static ModuleEndpointAllocator CreateUnixAllocator(string? sharedDirectory = null, string? dataRoot = null) =>
        new(
            ModuleTransportKind.UnixDomainSocket,
            sharedDirectory,
            "dotnetcloud",
            new ProcessSupervisorOptions(),
            dataRoot ?? Path.Combine(Path.GetTempPath(), "dnc-allocator"));

    [TestMethod]
    public void Resolve_UnixTransport_PlacesSocketInModuleRunDirectory()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "dnc-allocator-run");

        var endpoint = CreateUnixAllocator(dataRoot: dataRoot).Resolve("dotnetcloud.files");

        Assert.AreEqual(ModuleTransportKind.UnixDomainSocket, endpoint.Transport);
        Assert.AreEqual(
            Path.Combine(dataRoot, "modules", "dotnetcloud.files", "run", "module.sock"),
            endpoint.Address);
    }

    [TestMethod]
    public void Resolve_UnixTransportWithSharedDirectory_UsesModuleNamedFile()
    {
        var allocator = CreateUnixAllocator(sharedDirectory: "/run/dotnetcloud");

        var first = allocator.Resolve("dotnetcloud.files");
        var second = allocator.Resolve("dotnetcloud.chat");

        Assert.AreEqual("/run/dotnetcloud/dotnetcloud.files.sock", first.Address);
        Assert.AreEqual("/run/dotnetcloud/dotnetcloud.chat.sock", second.Address);
    }

    [TestMethod]
    public void Resolve_UnixTransportWithUnbindablePath_FallsBackToTcp()
    {
        // sockaddr_un.sun_path is ~108 bytes; a socket deeper than that cannot be bound.
        var dataRoot = Path.Combine(Path.GetTempPath(), new string('d', 120));

        var endpoint = CreateUnixAllocator(dataRoot: dataRoot).Resolve("dotnetcloud.files");

        Assert.AreEqual(ModuleTransportKind.Tcp, endpoint.Transport, "A too-long socket path must fall back to TCP rather than fail the bind.");
        Assert.IsTrue(endpoint.ToEndpointString().StartsWith("http://localhost:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Resolve_SocketPathStaysInsideTheSocketLengthLimit()
    {
        var endpoint = CreateUnixAllocator().Resolve("dotnetcloud.files");

        Assert.IsTrue(
            endpoint.Address.Length <= ModuleEndpointAllocator.MaxUnixSocketPathLength,
            $"Socket path '{endpoint.Address}' exceeds {ModuleEndpointAllocator.MaxUnixSocketPathLength} characters.");
    }

    [TestMethod]
    public void Resolve_NamedPipeTransport_UsesPrefixedModuleId()
    {
        var allocator = new ModuleEndpointAllocator(
            ModuleTransportKind.NamedPipe,
            unixSocketDirectory: null,
            namedPipePrefix: "dnc",
            new ProcessSupervisorOptions(),
            Path.GetTempPath());

        var endpoint = allocator.Resolve("dotnetcloud.files");

        Assert.AreEqual(ModuleTransportKind.NamedPipe, endpoint.Transport);
        Assert.AreEqual("dnc-dotnetcloud.files", endpoint.Address);
    }

    [TestMethod]
    public void Resolve_DifferentModules_GetDifferentEndpoints()
    {
        var allocator = CreateUnixAllocator();

        var files = allocator.Resolve("dotnetcloud.files");
        var chat = allocator.Resolve("dotnetcloud.chat");

        Assert.AreNotEqual(files.Address, chat.Address);
    }

    [TestMethod]
    public void Resolve_TcpTransport_IsDeterministic()
    {
        var allocator = new ModuleEndpointAllocator(
            ModuleTransportKind.Tcp,
            unixSocketDirectory: null,
            namedPipePrefix: "dotnetcloud",
            new ProcessSupervisorOptions(),
            Path.GetTempPath());

        var first = allocator.Resolve("dotnetcloud.files");
        var second = allocator.Resolve("dotnetcloud.files");

        Assert.AreEqual(first, second);
        Assert.AreEqual(ModuleTransportKind.Tcp, first.Transport);
    }

    [TestMethod]
    public void Resolve_TcpTransport_ProbesPastAnAllocatedPort()
    {
        var allocator = new ModuleEndpointAllocator(
            ModuleTransportKind.Tcp,
            unixSocketDirectory: null,
            namedPipePrefix: "dotnetcloud",
            new ProcessSupervisorOptions(),
            Path.GetTempPath());

        var preferred = allocator.Resolve("dotnetcloud.files");
        var taken = allocator.Resolve("dotnetcloud.files", new[] { preferred.ToEndpointString() });

        Assert.AreEqual(ModuleTransportKind.Tcp, taken.Transport);
        Assert.AreNotEqual(preferred.Address, taken.Address);

        // Module ids hash with a per-process randomised seed, so only assert the range.
        var options = new ProcessSupervisorOptions();
        foreach (var endpoint in new[] { preferred, taken })
        {
            var port = new Uri(endpoint.ToEndpointString()).Port;
            Assert.IsTrue(
                port > options.TcpPortRangeStart && port <= options.TcpPortRangeEnd,
                $"Port {port} is outside the module range.");
        }
    }

    [TestMethod]
    public void GetUnixSocketPath_ForTcpAllocator_IsNull()
    {
        var allocator = new ModuleEndpointAllocator(
            ModuleTransportKind.Tcp,
            unixSocketDirectory: null,
            namedPipePrefix: "dotnetcloud",
            new ProcessSupervisorOptions(),
            Path.GetTempPath());

        Assert.IsNull(allocator.GetUnixSocketPath("dotnetcloud.files"));
    }

    [TestMethod]
    public void Kind_ReflectsTheConfiguredTransport()
    {
        Assert.AreEqual(ModuleTransportKind.UnixDomainSocket, CreateUnixAllocator().Kind);
    }
}
