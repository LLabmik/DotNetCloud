using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChatApiClient"/> push-notification API methods.
/// </summary>
[TestClass]
public class ChatApiClientTests
{
    [TestMethod]
    public async Task RegisterDeviceAsync_WhenCalled_ThenPostsExpectedPayloadAndReturnsTrue()
    {
        var userId = Guid.NewGuid();
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return Task.FromResult(JsonResponse("{\"success\":true,\"data\":{\"registered\":true}}"));
        });

        var client = CreateClient(handler);
        var dto = new RegisterDeviceDto
        {
            DeviceToken = "token-123",
            Provider = "FCM"
        };

        var result = await client.RegisterDeviceAsync(userId, dto);

        Assert.IsTrue(result);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Post, capturedRequest.Method);
        Assert.AreEqual($"http://localhost/api/v1/notifications/devices/register?userId={userId}", capturedRequest.RequestUri?.ToString());

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var parsed = JsonDocument.Parse(body);
        Assert.AreEqual(dto.DeviceToken, parsed.RootElement.GetProperty("deviceToken").GetString());
        Assert.AreEqual(dto.Provider, parsed.RootElement.GetProperty("provider").GetString());
    }

    [TestMethod]
    public async Task UnregisterDeviceAsync_WhenCalled_ThenDeletesEncodedTokenRouteAndReturnsTrue()
    {
        var userId = Guid.NewGuid();
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return Task.FromResult(JsonResponse("{\"success\":true,\"data\":{\"unregistered\":true}}"));
        });

        var client = CreateClient(handler);
        var token = "tok/with space";

        var result = await client.UnregisterDeviceAsync(userId, token);

        Assert.IsTrue(result);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Delete, capturedRequest.Method);
        Assert.IsNotNull(capturedRequest.RequestUri);
        StringAssert.StartsWith(capturedRequest.RequestUri.AbsolutePath, "/api/v1/notifications/devices/tok%2Fwith");
        Assert.AreEqual($"?userId={userId}", capturedRequest.RequestUri.Query);
    }

    [TestMethod]
    public async Task GetNotificationPreferencesAsync_WhenCalled_ThenReturnsPreferencesFromEnvelope()
    {
        var userId = Guid.NewGuid();
        HttpRequestMessage? capturedRequest = null;
        const string responseJson = """
            {
              "success": true,
              "data": {
                "pushEnabled": true,
                "doNotDisturb": true,
                "mutedChannelIds": ["11111111-1111-1111-1111-111111111111"]
              }
            }
            """;

        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return Task.FromResult(JsonResponse(responseJson));
        });

        var client = CreateClient(handler);

        var result = await client.GetNotificationPreferencesAsync(userId);

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Get, capturedRequest.Method);
        Assert.AreEqual($"http://localhost/api/v1/notifications/preferences?userId={userId}", capturedRequest.RequestUri?.ToString());

        Assert.IsNotNull(result);
        Assert.IsTrue(result.PushEnabled);
        Assert.IsTrue(result.DoNotDisturb);
        Assert.AreEqual(1, result.MutedChannelIds.Count);
    }

    [TestMethod]
    public async Task UpdateNotificationPreferencesAsync_WhenCalled_ThenPutsPayloadAndReturnsTrue()
    {
        var userId = Guid.NewGuid();
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return Task.FromResult(JsonResponse("{\"success\":true,\"data\":{\"updated\":true}}"));
        });

        var client = CreateClient(handler);
        var dto = new NotificationPreferencesDto
        {
            PushEnabled = false,
            DoNotDisturb = true,
            MutedChannelIds = [Guid.NewGuid(), Guid.NewGuid()]
        };

        var result = await client.UpdateNotificationPreferencesAsync(userId, dto);

        Assert.IsTrue(result);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Put, capturedRequest.Method);
        Assert.AreEqual($"http://localhost/api/v1/notifications/preferences?userId={userId}", capturedRequest.RequestUri?.ToString());

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var parsed = JsonDocument.Parse(body);
        Assert.IsFalse(parsed.RootElement.GetProperty("pushEnabled").GetBoolean());
        Assert.IsTrue(parsed.RootElement.GetProperty("doNotDisturb").GetBoolean());
        Assert.AreEqual(2, parsed.RootElement.GetProperty("mutedChannelIds").GetArrayLength());
    }

    private static ChatApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        return new ChatApiClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
