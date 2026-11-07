using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.Services;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the content policy on module-sent notifications: bounded text, and an action link
/// that can only be an in-app path so a module cannot turn a notification into a phishing hop.
/// </summary>
[TestClass]
public class CoreCapabilitiesSendNotificationTests
{
    private static CoreCapabilitiesServiceImpl CreateService()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<INotificationService>());
        var provider = services.BuildServiceProvider();

        var indexingService = new SearchIndexingService(
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<SearchIndexingService>.Instance);

        return new CoreCapabilitiesServiceImpl(
            NullLogger<CoreCapabilitiesServiceImpl>.Instance,
            provider,
            indexingService,
            new ModuleEventInbox(
                Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
                Mock.Of<DotNetCloud.Core.Server.ModuleLoading.IModuleManifestRegistry>(),
                NullLogger<ModuleEventInbox>.Instance));
    }

    private static SendNotificationRequest CreateRequest(string? title = "Reminder", string? body = null, string? link = null)
    {
        var request = new SendNotificationRequest
        {
            Title = title ?? string.Empty,
            Body = body ?? string.Empty,
            Link = link ?? string.Empty,
        };

        request.RecipientUserIds.Add(Guid.CreateVersion7().ToString());
        return request;
    }

    [TestMethod]
    public async Task SendNotification_ExternalLink_IsRejected()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.SendNotification(
                CreateRequest(link: "https://evil.example/login"),
                new TestServerCallContext()));

        Assert.AreEqual(StatusCode.InvalidArgument, ex.StatusCode);
        Assert.AreEqual("MODULE_INVALID_REQUEST", ex.Trailers.GetValue("error-code"));
    }

    [TestMethod]
    public async Task SendNotification_ProtocolRelativeLink_IsRejected()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.SendNotification(CreateRequest(link: "//evil.example/x"), new TestServerCallContext()));

        Assert.AreEqual(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [TestMethod]
    public async Task SendNotification_JavascriptLink_IsRejected()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.SendNotification(CreateRequest(link: "javascript:alert(1)"), new TestServerCallContext()));

        Assert.AreEqual(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [TestMethod]
    public async Task SendNotification_OversizedTitle_IsRejected()
    {
        var service = CreateService();
        var title = new string('a', ModuleNotificationLimits.MaxTitleLength + 1);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.SendNotification(CreateRequest(title: title), new TestServerCallContext()));

        Assert.AreEqual(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [TestMethod]
    public async Task SendNotification_BlankTitle_IsRejected()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.SendNotification(CreateRequest(title: "   "), new TestServerCallContext()));

        Assert.AreEqual(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [TestMethod]
    public async Task SendNotification_ControlCharactersInBody_IsRejected()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.SendNotification(
                CreateRequest(body: "hello\nworld"), new TestServerCallContext()));

        Assert.AreEqual(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [TestMethod]
    public async Task SendNotification_RelativeLink_IsAccepted()
    {
        var service = CreateService();

        var response = await service.SendNotification(
            CreateRequest(link: "/calendar/events/123"),
            new TestServerCallContext());

        Assert.IsTrue(response.Success);
        Assert.AreEqual(1, response.DeliveredCount);
    }
}
