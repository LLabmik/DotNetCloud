using System.Net;
using System.Text;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Android.Push.Tests;

/// <summary>
/// Covers the wire shape of the endpoint registration: what is sent, where, and that nothing but
/// identifiers ever leaves the device.
/// </summary>
[TestClass]
public sealed class PushEndpointRegistrarTests
{
    private const string ServerUrl = "https://cloud.example.com";
    private const string Endpoint = "https://cloud.example.com/push/upCapabilityTopic";

    [TestMethod]
    public async Task RegisterAsync_PostsTheEndpointWithTheUnifiedPushProvider()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK);
        var registrar = NewRegistrar(handler);

        // Act
        var registered = await registrar.RegisterAsync(ServerUrl, Endpoint);

        // Assert
        Assert.IsTrue(registered);
        Assert.IsNotNull(handler.Request);
        Assert.AreEqual(HttpMethod.Post, handler.Request.Method);

        // A trailing slash on the connection URL must not produce a double slash.
        Assert.AreEqual(
            "https://cloud.example.com/api/v1/notifications/devices/register",
            handler.Request.RequestUri!.ToString());

        var body = handler.Body!;
        Assert.IsTrue(body.Contains($"\"deviceToken\":\"{Endpoint}\"", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("\"provider\":\"UnifiedPush\"", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("\"endpoint\":", StringComparison.Ordinal));

        // The registrar has no access to notification text at all — nothing else may be sent.
        Assert.IsFalse(body.Contains("title", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(body.Contains("body", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task RegisterAsync_TrailingSlashOnTheServerUrl_IsNormalized()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK);
        var registrar = NewRegistrar(handler);

        // Act
        await registrar.RegisterAsync($"{ServerUrl}/", Endpoint);

        // Assert
        Assert.AreEqual(
            "https://cloud.example.com/api/v1/notifications/devices/register",
            handler.Request!.RequestUri!.ToString());
    }

    [TestMethod]
    public async Task RegisterAsync_FailureStatus_ReturnsFalseWithoutThrowing()
    {
        // Arrange
        var registrar = NewRegistrar(new StubHandler(HttpStatusCode.InternalServerError));

        // Act
        var registered = await registrar.RegisterAsync(ServerUrl, Endpoint);

        // Assert
        Assert.IsFalse(registered);
    }

    [TestMethod]
    public async Task RegisterAsync_TransportFailure_ReturnsFalseWithoutThrowing()
    {
        // Arrange
        var registrar = NewRegistrar(new ThrowingHandler());

        // Act
        var registered = await registrar.RegisterAsync(ServerUrl, Endpoint);

        // Assert
        Assert.IsFalse(registered);
    }

    [TestMethod]
    public async Task RegisterAsync_MissingArguments_ReturnsFalseWithoutCallingTheServer()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK);
        var registrar = NewRegistrar(handler);

        // Act + Assert
        Assert.IsFalse(await registrar.RegisterAsync(string.Empty, Endpoint));
        Assert.IsFalse(await registrar.RegisterAsync(ServerUrl, "  "));
        Assert.IsNull(handler.Request);
    }

    [TestMethod]
    public async Task UnregisterAsync_DeletesTheEscapedEndpoint()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK);
        var registrar = NewRegistrar(handler);

        // Act
        var removed = await registrar.UnregisterAsync(ServerUrl, Endpoint);

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(HttpMethod.Delete, handler.Request!.Method);
        Assert.AreEqual(
            $"https://cloud.example.com/api/v1/notifications/devices/{Uri.EscapeDataString(Endpoint)}",
            handler.Request.RequestUri!.ToString());
    }

    [TestMethod]
    public async Task UnregisterAsync_AlreadyForgottenEndpoint_IsTreatedAsSuccess()
    {
        // Arrange
        var registrar = NewRegistrar(new StubHandler(HttpStatusCode.NotFound));

        // Act
        var removed = await registrar.UnregisterAsync(ServerUrl, Endpoint);

        // Assert
        Assert.IsTrue(removed);
    }

    private static PushEndpointRegistrar NewRegistrar(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<PushEndpointRegistrar>.Instance);

    private class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content is not null)
                Body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new HttpResponseMessage(status) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("no network");
    }
}
