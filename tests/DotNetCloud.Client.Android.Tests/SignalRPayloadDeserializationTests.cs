/// <summary>
/// Standalone validation of SignalR payload deserialization.
/// No platform-specific dependencies — validates JSON → DTO mapping works.
/// </summary>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Chat.Tests;

internal sealed record UnreadCountUpdatedPayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("count")] int Count);

internal sealed record NewMessagePayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("message")] string Message);

public sealed class SignalRPayloadDeserializationTests
{
    [Fact]
    public void UnreadCountUpdatedPayload_DeserializesFromJson()
    {
        // Arrange
        var json = """{"channelId":"ch-001","count":5}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var payload = JsonSerializer.Deserialize<UnreadCountUpdatedPayload>(json, options);

        // Assert
        Assert.NotNull(payload);
        Assert.Equal("ch-001", payload.ChannelId);
        Assert.Equal(5, payload.Count);
    }

    [Fact]
    public void NewMessagePayload_DeserializesFromJson()
    {
        // Arrange
        var json = """{"channelId":"ch-002","message":"Hello from server"}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var payload = JsonSerializer.Deserialize<NewMessagePayload>(json, options);

        // Assert
        Assert.NotNull(payload);
        Assert.Equal("ch-002", payload.ChannelId);
        Assert.Equal("Hello from server", payload.Message);
    }

    [Fact]
    public void UnreadPayload_WithNullChannel_Fails()
    {
        // Arrange
        var json = """{"channelId":null,"count":5}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act & Assert
        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<UnreadCountUpdatedPayload>(json, options));
    }

    [Fact]
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
        Assert.NotNull(payload1);
        Assert.NotNull(payload2);
        Assert.Equal("ch-001", payload1.ChannelId);
        Assert.Equal(3, payload1.Count);
        Assert.Equal("ch-002", payload2.ChannelId);
        Assert.Equal(7, payload2.Count);
    }
}
