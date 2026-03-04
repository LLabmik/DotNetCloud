using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Grpc.Lifecycle;
using Grpc.Net.Client;

namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// Provides factory methods for creating gRPC clients configured for integration testing.
/// </summary>
/// <remarks>
/// These helpers create typed gRPC clients connected to the test server's gRPC endpoint.
/// In the real system, modules communicate with the core over Unix sockets or Named Pipes;
/// in tests, we use an in-process HTTP handler provided by <see cref="DotNetCloudWebApplicationFactory"/>.
/// </remarks>
internal static class GrpcTestClientFactory
{
    /// <summary>
    /// Creates a <see cref="ModuleLifecycle.ModuleLifecycleClient"/> connected to the test server.
    /// </summary>
    /// <param name="factory">The <see cref="DotNetCloudWebApplicationFactory"/> hosting the test server.</param>
    /// <returns>A configured lifecycle gRPC client.</returns>
    public static ModuleLifecycle.ModuleLifecycleClient CreateLifecycleClient(
        DotNetCloudWebApplicationFactory factory)
    {
        var channel = CreateChannel(factory);
        return new ModuleLifecycle.ModuleLifecycleClient(channel);
    }

    /// <summary>
    /// Creates a <see cref="CoreCapabilities.CoreCapabilitiesClient"/> connected to the test server.
    /// </summary>
    /// <param name="factory">The <see cref="DotNetCloudWebApplicationFactory"/> hosting the test server.</param>
    /// <returns>A configured capabilities gRPC client.</returns>
    public static CoreCapabilities.CoreCapabilitiesClient CreateCapabilitiesClient(
        DotNetCloudWebApplicationFactory factory)
    {
        var channel = CreateChannel(factory);
        return new CoreCapabilities.CoreCapabilitiesClient(channel);
    }

    /// <summary>
    /// Creates a <see cref="CallerContextMessage"/> for a module caller in tests.
    /// </summary>
    /// <param name="moduleId">The module ID (e.g., "dotnetcloud.example").</param>
    /// <param name="userId">Optional user ID context. Defaults to a new GUID.</param>
    public static CallerContextMessage CreateModuleCaller(
        string moduleId = "dotnetcloud.test",
        Guid? userId = null)
    {
        return new CallerContextMessage
        {
            UserId = (userId ?? Guid.NewGuid()).ToString(),
            CallerType = "Module",
            ModuleId = moduleId,
            Roles = { "User" },
        };
    }

    /// <summary>
    /// Creates a <see cref="CallerContextMessage"/> for a system caller in tests.
    /// </summary>
    public static CallerContextMessage CreateSystemCaller()
    {
        return new CallerContextMessage
        {
            UserId = Guid.Empty.ToString(),
            CallerType = "System",
        };
    }

    private static GrpcChannel CreateChannel(DotNetCloudWebApplicationFactory factory)
    {
        var httpClient = factory.CreateDefaultClient();

        return GrpcChannel.ForAddress(httpClient.BaseAddress!, new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler(),
        });
    }
}
