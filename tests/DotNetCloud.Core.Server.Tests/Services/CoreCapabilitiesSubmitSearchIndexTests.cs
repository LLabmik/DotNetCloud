using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="CoreCapabilitiesServiceImpl.SubmitSearchIndex"/>.
/// </summary>
[TestClass]
public class CoreCapabilitiesSubmitSearchIndexTests
{
    private static CoreCapabilitiesServiceImpl CreateService(SearchIndexingService indexingService)
        => new(
            NullLogger<CoreCapabilitiesServiceImpl>.Instance,
            Mock.Of<IServiceProvider>(),
            indexingService,
            CreateEventInbox(),
            ConsentTestDoubles.NoConsent());

    private static ModuleEventInbox CreateEventInbox()
        => new(
            Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
            Mock.Of<DotNetCloud.Core.Server.ModuleLoading.IModuleManifestRegistry>(),
            NullLogger<ModuleEventInbox>.Instance);

    private static SearchIndexingService CreateIndexingService()
        => new(
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<SearchIndexingService>.Instance);

    private static SubmitSearchIndexRequest CreateRequest(int action, string moduleId = "notes")
        => new()
        {
            ModuleId = moduleId,
            EntityId = Guid.CreateVersion7().ToString(),
            Action = action
        };

    [TestMethod]
    public async Task SubmitSearchIndex_IndexAction_EnqueuesAndReturnsSuccess()
    {
        var indexingService = CreateIndexingService();
        var service = CreateService(indexingService);

        var response = await service.SubmitSearchIndex(
            CreateRequest(action: 0 /* SearchIndexAction.Index */),
            new TestServerCallContext());

        Assert.IsTrue(response.Success);
        Assert.AreEqual(1, indexingService.PendingCount);
    }

    [TestMethod]
    public async Task SubmitSearchIndex_RemoveAction_EnqueuesAndReturnsSuccess()
    {
        var indexingService = CreateIndexingService();
        var service = CreateService(indexingService);

        var response = await service.SubmitSearchIndex(
            CreateRequest(action: 1 /* SearchIndexAction.Remove */),
            new TestServerCallContext());

        Assert.IsTrue(response.Success);
        Assert.AreEqual(1, indexingService.PendingCount);
    }

    [TestMethod]
    public async Task SubmitSearchIndex_InvalidAction_ReturnsFailureAndDoesNotEnqueue()
    {
        var indexingService = CreateIndexingService();
        var service = CreateService(indexingService);

        var response = await service.SubmitSearchIndex(
            CreateRequest(action: 42),
            new TestServerCallContext());

        Assert.IsFalse(response.Success);
        Assert.AreEqual(0, indexingService.PendingCount);
    }

    [TestMethod]
    public async Task SubmitSearchIndex_RequestModuleIdDiffersFromAuthenticatedModule_UsesAuthenticatedModuleId()
    {
        // A module must not be able to index, overwrite or remove entries attributed to another
        // module. The index entry is stamped from the credential-verified module identity.
        var indexingService = CreateIndexingService();
        var service = CreateService(indexingService);
        var context = new TestServerCallContext();
        context.UserState["ModuleId"] = "dotnetcloud.notes";

        var response = await service.SubmitSearchIndex(
            CreateRequest(action: 0, moduleId: "dotnetcloud.chat"),
            context);

        Assert.IsTrue(response.Success);
        Assert.IsTrue(indexingService.TryPeekPending(out var pending));
        Assert.AreEqual("dotnetcloud.notes", pending!.ModuleId);
    }
}
