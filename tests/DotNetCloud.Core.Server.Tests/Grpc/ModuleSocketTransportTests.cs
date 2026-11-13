using System.Net;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Supervisor;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// End-to-end tests for the phase A4 socket transport: a real Kestrel HTTP/2 listener bound to a
/// Unix domain socket, reached through the core's <c>ConnectCallback</c> transports.
/// </summary>
/// <remarks>
/// <para>
/// These are the tests that would have caught the original defect: hosts parsed
/// <c>unix:///path</c> into <c>http:///path</c> and bound <c>127.0.0.1:80</c>, so nothing actually
/// listened on the socket.
/// </para>
/// <para>
/// The module credential is still the API identity control on a socket. A Unix socket is reachable
/// by any process running as the same OS user — that is, by sibling modules — so the tests below
/// prove that a caller without the supervisor-issued token is rejected even though it can open the
/// socket.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ModuleSocketTransportTests
{
    private const string ModuleId = "dotnetcloud.files";
    private const string ModuleToken = "socket-transport-token";

    private string _root = string.Empty;
    private string _socketPath = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "dnc-socket-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _socketPath = Path.Combine(_root, "module.sock");
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
            // Best effort: the listening socket is removed when the host disposes.
        }
    }

    [TestMethod]
    public async Task HttpOverUnixSocket_PingRoundTrips()
    {
        await using var host = await StartHttpHostAsync();

        using var handler = ModuleHttpTransport.CreateHandler(ModuleEndpoint.UnixDomainSocket(_socketPath));
        using var client = new HttpClient(handler)
        {
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        var response = await client.GetAsync("http://localhost/ping");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("pong", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task GrpcOverUnixSocket_WithModuleCredential_ReachesTheHost()
    {
        // Mirrors the supervisor: it mints the token, passes it to the host as an env var, and the
        // core's channel echoes it back on every call.
        var credentials = new ModuleCredentialStore();
        var issued = credentials.Issue(ModuleId);
        await using var host = await StartGrpcHostAsync(issued);

        using var manager = new GrpcChannelManager(NullLogger<GrpcChannelManager>.Instance, credentials);

        var channel = manager.GetOrCreateChannel(ModuleId, ModuleEndpoint.UnixDomainSocket(_socketPath).ToEndpointString());
        var client = new ModuleLifecycle.ModuleLifecycleClient(channel);

        var response = await client.HealthCheckAsync(new HealthCheckRequest());

        Assert.AreEqual(HealthStatus.Healthy, response.Status);
    }

    [TestMethod]
    public async Task GrpcOverUnixSocket_WithoutCredential_IsRejected()
    {
        await using var host = await StartGrpcHostAsync(ModuleToken);

        // The store never issued a token for this module, so the call carries no metadata.
        using var manager = new GrpcChannelManager(NullLogger<GrpcChannelManager>.Instance, new ModuleCredentialStore());
        var channel = manager.GetOrCreateChannel(ModuleId, ModuleEndpoint.UnixDomainSocket(_socketPath).ToEndpointString());
        var client = new ModuleLifecycle.ModuleLifecycleClient(channel);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            async () => await client.HealthCheckAsync(new HealthCheckRequest()));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    public async Task GrpcOverUnixSocket_WithAnotherModulesCredential_IsRejected()
    {
        var hostCredentials = new ModuleCredentialStore();
        await using var host = await StartGrpcHostAsync(hostCredentials.Issue(ModuleId));

        // Socket reachability is not trust: a sibling module can open this socket, but it cannot
        // present this module's credential.
        var foreignCredentials = new ModuleCredentialStore();
        foreignCredentials.Issue("dotnetcloud.chat");
        using var manager = new GrpcChannelManager(NullLogger<GrpcChannelManager>.Instance, foreignCredentials);

        var channel = manager.GetOrCreateChannel(ModuleId, ModuleEndpoint.UnixDomainSocket(_socketPath).ToEndpointString());
        var client = new ModuleLifecycle.ModuleLifecycleClient(channel);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            async () => await client.HealthCheckAsync(new HealthCheckRequest()));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    public async Task GrpcOverTcpTransport_StillWorks()
    {
        // TCP fallback path (Modules:Transport=Tcp) must keep working after the socket switch.
        var port = FindFreeLoopbackPort();
        var credentials = new ModuleCredentialStore();
        var issued = credentials.Issue(ModuleId);
        await using var host = await StartGrpcHostAsync(issued, ModuleEndpoint.Tcp(port));

        using var manager = new GrpcChannelManager(NullLogger<GrpcChannelManager>.Instance, credentials);

        var channel = manager.GetOrCreateChannel(ModuleId, $"http://localhost:{port}");
        var client = new ModuleLifecycle.ModuleLifecycleClient(channel);

        var response = await client.HealthCheckAsync(new HealthCheckRequest());

        Assert.AreEqual(HealthStatus.Healthy, response.Status);
    }

    [TestMethod]
    public async Task ConnectAsync_TcpEndpoint_IsNotSupported()
    {
        await Assert.ThrowsExactlyAsync<NotSupportedException>(
            async () => await ModuleHttpTransport.ConnectAsync(ModuleEndpoint.Tcp(50105), CancellationToken.None));
    }

    [TestMethod]
    public void ModuleHttpInvokerCache_TcpEndpoint_UsesLiveAuthority()
    {
        using var cache = new ModuleHttpInvokerCache();

        var (prefix, invoker) = cache.Resolve(ModuleId, "http://localhost:50105");

        Assert.AreEqual("http://localhost:50105/", prefix);
        Assert.IsNotNull(invoker);
    }

    [TestMethod]
    public void ModuleHttpInvokerCache_SocketEndpoint_UsesLoopbackAuthority()
    {
        using var cache = new ModuleHttpInvokerCache();
        var endpoint = ModuleEndpoint.UnixDomainSocket(_socketPath).ToEndpointString();

        var (prefix, invoker) = cache.Resolve(ModuleId, endpoint);

        Assert.AreEqual("http://localhost/", prefix);
        Assert.IsNotNull(invoker);
    }

    [TestMethod]
    public void ModuleHttpInvokerCache_SocketEndpoint_ReusesInvokerPerModule()
    {
        using var cache = new ModuleHttpInvokerCache();
        var endpoint = ModuleEndpoint.UnixDomainSocket(_socketPath).ToEndpointString();

        var first = cache.Resolve(ModuleId, endpoint);
        var second = cache.Resolve(ModuleId, endpoint);

        Assert.AreSame(first.Invoker, second.Invoker, "Socket handlers are per module; a second resolve must not rebuild one.");

        cache.Invalidate(ModuleId);
        var third = cache.Resolve(ModuleId, endpoint);
        Assert.AreNotSame(first.Invoker, third.Invoker, "Invalidate must drop the per-module handler.");
    }

    // ---- Helpers ----

    private static int FindFreeLoopbackPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class TestLifecycleService : ModuleLifecycle.ModuleLifecycleBase
    {
        public override Task<HealthCheckResponse> HealthCheck(
            HealthCheckRequest request,
            ServerCallContext context) =>
            Task.FromResult(new HealthCheckResponse { Status = HealthStatus.Healthy });
    }

    private async Task<AsyncDisposableHost> StartHttpHostAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => ModuleGrpcEndpointBinding.Bind(kestrel, ModuleEndpoint.UnixDomainSocket(_socketPath)));

        var app = builder.Build();
        app.MapGet("/ping", () => "pong");

        await app.StartAsync();
        return new AsyncDisposableHost(app);
    }

    private async Task<AsyncDisposableHost> StartGrpcHostAsync(string expectedToken, ModuleEndpoint? endpoint = null)
    {
        var previous = Environment.GetEnvironmentVariable(ModuleIdentityMetadata.ModuleTokenEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(ModuleIdentityMetadata.ModuleTokenEnvironmentVariable, expectedToken);

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(kestrel =>
                ModuleGrpcEndpointBinding.Bind(kestrel, endpoint ?? ModuleEndpoint.UnixDomainSocket(_socketPath)));
            builder.Services.AddGrpc(options =>
                options.Interceptors.Add<ModulePeerAuthenticationInterceptor>());

            var app = builder.Build();
            app.MapGrpcService<TestLifecycleService>();

            await app.StartAsync();
            return new AsyncDisposableHost(app, () => Environment.SetEnvironmentVariable(
                ModuleIdentityMetadata.ModuleTokenEnvironmentVariable, previous));
        }
        catch
        {
            Environment.SetEnvironmentVariable(ModuleIdentityMetadata.ModuleTokenEnvironmentVariable, previous);
            throw;
        }
    }

    private sealed class AsyncDisposableHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly Action? _onDispose;

        public AsyncDisposableHost(WebApplication app, Action? onDispose = null)
        {
            _app = app;
            _onDispose = onDispose;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _app.StopAsync();
            }
            finally
            {
                await _app.DisposeAsync();
                _onDispose?.Invoke();
            }
        }
    }
}
