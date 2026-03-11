// Minimal standalone validator for SignalR payload deserialization
// Tests JSON parsing without platform dependencies

using System.Text.Json;
using System.Text.Json.Serialization;

var passed = 0;
var failed = 0;

Console.WriteLine("SignalR Payload Deserialization Tests\n" + new string('=', 50));

// Test 1: UnreadCountUpdatedPayload
try
{
    var json = """{"channelId":"ch-001","count":5}""";
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var payload = JsonSerializer.Deserialize<UnreadCountUpdatedPayload>(json, options);
    
    if (payload != null && payload.ChannelId == "ch-001" && payload.Count == 5)
    {
        Console.WriteLine("✓ Test 1: UnreadCountUpdatedPayload deserialization");
        passed++;
    }
    else
    {
        Console.WriteLine("✗ Test 1: Payload values mismatch");
        failed++;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Test 1: {ex.Message}");
    failed++;
}

// Test 2: NewMessagePayload
try
{
    var json = """{"channelId":"ch-002","message":"Hello from server"}""";
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var payload = JsonSerializer.Deserialize<NewMessagePayload>(json, options);
    
    if (payload != null && payload.ChannelId == "ch-002" && payload.Message == "Hello from server")
    {
        Console.WriteLine("✓ Test 2: NewMessagePayload deserialization");
        passed++;
    }
    else
    {
        Console.WriteLine("✗ Test 2: Payload values mismatch");
        failed++;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Test 2: {ex.Message}");
    failed++;
}

// Test 3: Multiple payloads independently
try
{
    var json1 = """{"channelId":"ch-001","count":3}""";
    var json2 = """{"channelId":"ch-002","count":7}""";
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    
    var payload1 = JsonSerializer.Deserialize<UnreadCountUpdatedPayload>(json1, options);
    var payload2 = JsonSerializer.Deserialize<UnreadCountUpdatedPayload>(json2, options);
    
    if (payload1?.ChannelId == "ch-001" && payload1.Count == 3 &&
        payload2?.ChannelId == "ch-002" && payload2.Count == 7)
    {
        Console.WriteLine("✓ Test 3: Multiple payload independence");
        passed++;
    }
    else
    {
        Console.WriteLine("✗ Test 3: Payload independence failed");
        failed++;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Test 3: {ex.Message}");
    failed++;
}

Console.WriteLine(new string('=', 50));
Console.WriteLine($"Results: {passed} passed, {failed} failed");
Environment.Exit(failed > 0 ? 1 : 0);

// Minimal payload DTOs for testing
internal sealed record UnreadCountUpdatedPayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("count")] int Count);

internal sealed record NewMessagePayload(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("message")] string Message);
