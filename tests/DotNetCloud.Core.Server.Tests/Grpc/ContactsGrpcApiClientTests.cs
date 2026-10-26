using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Grpc.Clients;
using DotNetCloud.Core.Server.Supervisor;
using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ContactsProto = DotNetCloud.Modules.Contacts.Host.Protos;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Regression tests for the core's Contacts gRPC client.
/// </summary>
/// <remarks>
/// <c>ContactsGrpcApiClient</c> is registered scoped and used to dispose its channel in
/// <c>Dispose()</c> — but that channel comes from <c>IModuleGrpcChannelProvider</c> and is shared by
/// every caller. The first scope to end (a closed Blazor circuit, a finished request) therefore
/// destroyed the contacts channel for the whole process, after which every call and the supervisor's
/// health probe failed with "Cannot access a disposed object. GrpcChannel" until the module was
/// restarted. On a live box that surfaced as a permanent 503 on the Contacts page.
/// </remarks>
[TestClass]
public class ContactsGrpcApiClientTests
{
    private sealed class FixedChannelProvider : IModuleGrpcChannelProvider
    {
        private readonly GrpcChannel _channel;

        public FixedChannelProvider(GrpcChannel channel) => _channel = channel;

        public int Requests { get; private set; }

        public GrpcChannel GetOrCreateChannel(string moduleId, string endpoint)
        {
            Requests++;
            return _channel;
        }
    }

    private static (ContactsGrpcApiClient Client, GrpcChannel Channel) CreateClient()
    {
        // Port 1 is never listening: calls fail fast with Unavailable, which is all this test needs.
        var channel = GrpcChannel.ForAddress("http://127.0.0.1:1");

        var endpointProvider = new ModuleEndpointProvider(
            Mock.Of<IProcessSupervisor>(),
            new FixedChannelProvider(channel),
            Options.Create(new ProcessSupervisorOptions()),
            NullLogger<ModuleEndpointProvider>.Instance);

        var client = new ContactsGrpcApiClient(
            Options.Create(new ContactsGrpcClientOptions()),
            endpointProvider,
            new FixedChannelProvider(channel),
            new HttpContextAccessor(),
            NullLogger<ContactsGrpcApiClient>.Instance);

        return (client, channel);
    }

    [TestMethod]
    public async Task Dispose_LeavesTheSharedChannelUsable()
    {
        var (client, channel) = CreateClient();

        // Force the lazy channel to be created, so a disposal bug would actually fire.
        await client.GetRecentContactsAsync();

        client.Dispose();

        // A second caller (the health probe, another scope, the next request) must still be able to
        // use the shared channel: it fails with Unavailable because nothing is listening, and in
        // particular not with ObjectDisposedException.
        var probe = new ContactsProto.ContactsService.ContactsServiceClient(channel);
        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => probe.GetRecentContactsAsync(new ContactsProto.GetRecentContactsRequest()).ResponseAsync);

        Assert.AreEqual(StatusCode.Unavailable, ex.StatusCode);
    }

    [TestMethod]
    public async Task Dispose_IsIdempotent()
    {
        var (client, channel) = CreateClient();
        await client.GetRecentContactsAsync();

        client.Dispose();
        client.Dispose();

        var probe = new ContactsProto.ContactsService.ContactsServiceClient(channel);
        await Assert.ThrowsExactlyAsync<RpcException>(
            () => probe.GetRecentContactsAsync(new ContactsProto.GetRecentContactsRequest()).ResponseAsync);
    }
}
