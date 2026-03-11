// Standalone validation of SignalR payload deserialization.
// No platform-specific dependencies; validates JSON-to-DTO mapping.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Android.Chat.Tests;

internal sealed record UnreadCountUpdatedPayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("count")] int Count);

internal sealed record NewMessagePayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("message")] string Message);

[TestClass]
public sealed class SignalRPayloadDeserializationTests
{
    [TestMethod]
    public void UnreadCountUpdatedPayload_DeserializesFromJson()
    {
        // Arrange
        var json = """{"channelId":"ch-001","count":5}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var payload = JsonSerializer.Deserialize<UnreadCountUpdatedPayload>(json, options);

        // Assert
        Assert.IsNotNull(payload);
        Assert.AreEqual("ch-001", payload.ChannelId);
        Assert.AreEqual(5, payload.Count);
    }

    [TestMethod]
    public void NewMessagePayload_DeserializesFromJson()
    {
        // Arrange
        var json = """{"channelId":"ch-002","message":"Hello from server"}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var payload = JsonSerializer.Deserialize<NewMessagePayload>(json, options);

        // Assert
        Assert.IsNotNull(payload);
        Assert.AreEqual("ch-002", payload.ChannelId);
        Assert.AreEqual("Hello from server", payload.Message);
    }

    [TestMethod]
    public void UnreadPayload_MissingCount_DefaultsToZero()
    {
        // Arrange
        var json = """{"channelId":"ch-003"}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var payload = JsonSerializer.Deserialize<UnreadCountUpdatedPayload>(json, options);

        // Assert
        Assert.IsNotNull(payload);
        Assert.AreEqual("ch-003", payload.ChannelId);
        Assert.AreEqual(0, payload.Count);
    }

    [TestMethod]
    public void MultiplePayloads_DeserializeIndependently()
    {
        // Arrange
        var json1 = """{"channelId":"ch-001","count":3}""";
        var json2 = """{"channelId":"ch-002","count":7}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var payload1 = JsonSerializer.Deserialize<UnreadCountUpdatedPayload>(json1, options);
        var payload2 = JsonSerializer.Deserialize<UnreadCountUpdatedPayload>(json2, options);

        // Assert
        Assert.IsNotNull(payload1);
        Assert.IsNotNull(payload2);
        Assert.AreEqual("ch-001", payload1.ChannelId);
        Assert.AreEqual(3, payload1.Count);
        Assert.AreEqual("ch-002", payload2.ChannelId);
        Assert.AreEqual(7, payload2.Count);
    }
}
