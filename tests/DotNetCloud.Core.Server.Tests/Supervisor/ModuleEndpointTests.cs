using DotNetCloud.Core.Modules.Supervisor;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// Tests for <see cref="ModuleEndpoint"/> parsing and formatting.
/// </summary>
/// <remarks>
/// The regression these pin: every module host used to do
/// <c>new Uri(endpoint.Replace("unix://", "http://"))</c> and bind <c>uri.Port</c>, which turned a
/// socket endpoint into loopback port 80 and silently discarded the socket path.
/// </remarks>
[TestClass]
public class ModuleEndpointTests
{
    [TestMethod]
    public void TryParse_LoopbackHttpEndpoint_ReturnsTcp()
    {
        Assert.IsTrue(ModuleEndpoint.TryParse("http://localhost:50105", out var endpoint));

        Assert.AreEqual(ModuleTransportKind.Tcp, endpoint.Transport);
        Assert.AreEqual("localhost:50105", endpoint.Address);
        Assert.IsFalse(endpoint.IsSocketTransport);
    }

    [TestMethod]
    public void TryParse_HttpsEndpoint_KeepsPortAndHost()
    {
        Assert.IsTrue(ModuleEndpoint.TryParse("https://127.0.0.1:5443", out var endpoint));

        Assert.AreEqual(ModuleTransportKind.Tcp, endpoint.Transport);
        Assert.AreEqual(5443, new Uri(endpoint.ToEndpointString()).Port);
    }

    [TestMethod]
    public void TryParse_UnixSocketEndpoint_ReturnsAbsolutePath()
    {
        Assert.IsTrue(ModuleEndpoint.TryParse("unix:///data/modules/x/run/module.sock", out var endpoint));

        Assert.AreEqual(ModuleTransportKind.UnixDomainSocket, endpoint.Transport);
        Assert.AreEqual("/data/modules/x/run/module.sock", endpoint.Address);
        Assert.IsTrue(endpoint.IsSocketTransport);
    }

    [TestMethod]
    public void TryParse_UnixSocketSingleSlashForm_ReturnsAbsolutePath()
    {
        Assert.IsTrue(ModuleEndpoint.TryParse("unix:/tmp/module.sock", out var endpoint));

        Assert.AreEqual(ModuleTransportKind.UnixDomainSocket, endpoint.Transport);
        Assert.AreEqual("/tmp/module.sock", endpoint.Address);
    }

    [TestMethod]
    public void TryParse_NamedPipeEndpoint_ReturnsPipeName()
    {
        Assert.IsTrue(ModuleEndpoint.TryParse("net.pipe://dotnetcloud-dotnetcloud.files", out var endpoint));

        Assert.AreEqual(ModuleTransportKind.NamedPipe, endpoint.Transport);
        Assert.AreEqual("dotnetcloud-dotnetcloud.files", endpoint.Address);
        Assert.IsTrue(endpoint.IsSocketTransport);
    }

    [TestMethod]
    public void TryParse_LegacyPipeScheme_ReturnsNamedPipe()
    {
        Assert.IsTrue(ModuleEndpoint.TryParse("pipe://dotnetcloud-chat", out var endpoint));

        Assert.AreEqual(ModuleTransportKind.NamedPipe, endpoint.Transport);
        Assert.AreEqual("dotnetcloud-chat", endpoint.Address);
    }

    [TestMethod]
    public void TryParse_BareHostPort_ReturnsTcp()
    {
        Assert.IsTrue(ModuleEndpoint.TryParse("localhost:50110", out var endpoint));

        Assert.AreEqual(ModuleTransportKind.Tcp, endpoint.Transport);
        Assert.AreEqual(50110, new Uri(endpoint.ToEndpointString()).Port);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not-an-endpoint")]
    [DataRow("unix://")]
    [DataRow("net.pipe://")]
    [DataRow("localhost:not-a-port")]
    public void TryParse_UnrecognisedValue_ReturnsFalse(string value)
    {
        Assert.IsFalse(ModuleEndpoint.TryParse(value, out var endpoint));
        Assert.IsNull(endpoint);
    }

    [TestMethod]
    public void TryParse_Null_ReturnsFalse()
    {
        Assert.IsFalse(ModuleEndpoint.TryParse(null, out _));
    }

    [TestMethod]
    public void ToEndpointString_RoundTripsEveryTransport()
    {
        var tcp = ModuleEndpoint.Tcp(50105);
        var socket = ModuleEndpoint.UnixDomainSocket("/data/modules/x/run/module.sock");
        var pipe = ModuleEndpoint.NamedPipe("dotnetcloud-x");

        Assert.AreEqual("http://localhost:50105", tcp.ToEndpointString());
        Assert.AreEqual("unix:///data/modules/x/run/module.sock", socket.ToEndpointString());
        Assert.AreEqual("net.pipe://dotnetcloud-x", pipe.ToEndpointString());

        foreach (var original in new[] { tcp, socket, pipe })
        {
            Assert.IsTrue(ModuleEndpoint.TryParse(original.ToEndpointString(), out var reparsed));
            Assert.AreEqual(original, reparsed);
        }
    }

    [TestMethod]
    public void ToClientBaseAddress_SocketTransport_IsLoopbackHttp()
    {
        var socket = ModuleEndpoint.UnixDomainSocket("/data/modules/x/run/module.sock");
        var pipe = ModuleEndpoint.NamedPipe("dotnetcloud-x");

        Assert.AreEqual("http://localhost", socket.ToClientBaseAddress());
        Assert.AreEqual("http://localhost", pipe.ToClientBaseAddress());
    }

    [TestMethod]
    public void ToClientBaseAddress_TcpTransport_KeepsRealAuthority()
    {
        Assert.AreEqual("http://localhost:50105", ModuleEndpoint.Tcp(50105).ToClientBaseAddress());
    }

    [TestMethod]
    public void Parse_UnrecognisedValue_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => ModuleEndpoint.Parse("garbage"));
    }
}
