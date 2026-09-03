using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="SearchIndexEventBridgeHandler"/>.
/// </summary>
[TestClass]
public class SearchIndexEventBridgeHandlerTests
{
    private static SearchIndexEventBridgeHandler CreateHandler(
        Mock<CoreCapabilities.CoreCapabilitiesClient> client)
        => new(client.Object, NullLogger<SearchIndexEventBridgeHandler>.Instance);

    private static AsyncUnaryCall<SubmitSearchIndexResponse> CreateUnaryCall(
        SubmitSearchIndexResponse response)
        => new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    private static SearchIndexRequestEvent CreateEvent(SearchIndexAction action = SearchIndexAction.Index)
        => new()
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "notes",
            EntityId = Guid.CreateVersion7().ToString(),
            Action = action
        };

    [TestMethod]
    public async Task HandleAsync_IndexAction_SubmitsIndexRequestToCore()
    {
        var @event = CreateEvent(SearchIndexAction.Index);
        var client = new Mock<CoreCapabilities.CoreCapabilitiesClient>();
        client
            .Setup(c => c.SubmitSearchIndexAsync(
                It.IsAny<SubmitSearchIndexRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateUnaryCall(new SubmitSearchIndexResponse { Success = true }));

        var handler = CreateHandler(client);

        await handler.HandleAsync(@event);

        client.Verify(c => c.SubmitSearchIndexAsync(
            It.Is<SubmitSearchIndexRequest>(r =>
                r.ModuleId == @event.ModuleId &&
                r.EntityId == @event.EntityId &&
                r.Action == (int)SearchIndexAction.Index),
            It.IsAny<Metadata>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_RemoveAction_MapsActionToRemove()
    {
        var @event = CreateEvent(SearchIndexAction.Remove);
        var client = new Mock<CoreCapabilities.CoreCapabilitiesClient>();
        client
            .Setup(c => c.SubmitSearchIndexAsync(
                It.IsAny<SubmitSearchIndexRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateUnaryCall(new SubmitSearchIndexResponse { Success = true }));

        var handler = CreateHandler(client);

        await handler.HandleAsync(@event);

        client.Verify(c => c.SubmitSearchIndexAsync(
            It.Is<SubmitSearchIndexRequest>(r =>
                r.ModuleId == @event.ModuleId &&
                r.EntityId == @event.EntityId &&
                r.Action == (int)SearchIndexAction.Remove),
            It.IsAny<Metadata>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_AttachesModuleIdHeader()
    {
        const string expectedModuleId = "dotnetcloud.notes";
        Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULE_ID", expectedModuleId);
        try
        {
            var @event = CreateEvent();
            var client = new Mock<CoreCapabilities.CoreCapabilitiesClient>();
            client
                .Setup(c => c.SubmitSearchIndexAsync(
                    It.IsAny<SubmitSearchIndexRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateUnaryCall(new SubmitSearchIndexResponse { Success = true }));

            var handler = CreateHandler(client);

            await handler.HandleAsync(@event);

            // Core.Server's AuthenticationInterceptor rejects gRPC calls without a
            // module-id header, so the bridge must attach the host's module identity.
            client.Verify(c => c.SubmitSearchIndexAsync(
                It.IsAny<SubmitSearchIndexRequest>(),
                It.Is<Metadata>(m => m.GetValue("module-id") == expectedModuleId),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULE_ID", null);
        }
    }

    [TestMethod]
    public async Task HandleAsync_CoreRejectsRequest_DoesNotThrow()
    {
        var @event = CreateEvent();
        var client = new Mock<CoreCapabilities.CoreCapabilitiesClient>();
        client
            .Setup(c => c.SubmitSearchIndexAsync(
                It.IsAny<SubmitSearchIndexRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateUnaryCall(new SubmitSearchIndexResponse { Success = false }));

        var handler = CreateHandler(client);

        // A rejected request is logged, never thrown.
        await handler.HandleAsync(@event);
    }

    [TestMethod]
    public async Task HandleAsync_CoreUnavailable_DoesNotThrow()
    {
        var @event = CreateEvent();
        var client = new Mock<CoreCapabilities.CoreCapabilitiesClient>();
        client
            .Setup(c => c.SubmitSearchIndexAsync(
                It.IsAny<SubmitSearchIndexRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "core unavailable")));

        var handler = CreateHandler(client);

        // Real-time indexing must never break module CRUD operations.
        await handler.HandleAsync(@event);
    }
}
