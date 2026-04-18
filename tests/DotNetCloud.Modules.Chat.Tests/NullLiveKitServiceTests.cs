using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for <see cref="NullLiveKitService"/> — the graceful-degradation
/// fallback used when LiveKit SFU is not configured.
/// </summary>
[TestClass]
public class NullLiveKitServiceTests
{
    private NullLiveKitService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new NullLiveKitService(NullLogger<NullLiveKitService>.Instance);
    }

    // ══════════════════════════════════════════════════════════════
    //  IsAvailable Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void IsAvailable_ReturnsFalse()
    {
        Assert.IsFalse(_service.IsAvailable);
    }

    // ══════════════════════════════════════════════════════════════
    //  MaxP2PParticipants Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void MaxP2PParticipants_DefaultsToThree()
    {
        Assert.AreEqual(3, _service.MaxP2PParticipants);
    }

    [TestMethod]
    public void MaxP2PParticipants_RespectsCustomValue()
    {
        var service = new NullLiveKitService(NullLogger<NullLiveKitService>.Instance, maxP2PParticipants: 5);
        Assert.AreEqual(5, service.MaxP2PParticipants);
    }

    [TestMethod]
    public void MaxP2PParticipants_RespectsValueOfOne()
    {
        var service = new NullLiveKitService(NullLogger<NullLiveKitService>.Instance, maxP2PParticipants: 1);
        Assert.AreEqual(1, service.MaxP2PParticipants);
    }

    // ══════════════════════════════════════════════════════════════
    //  CreateRoomAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateRoomAsync_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.CreateRoomAsync(Guid.NewGuid(), 10));
    }

    [TestMethod]
    public async Task CreateRoomAsync_ExceptionMessageContainsMaxParticipants()
    {
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.CreateRoomAsync(Guid.NewGuid(), 10));

        Assert.IsTrue(ex.Message.Contains("3"), "Exception message should mention the P2P participant limit.");
    }

    [TestMethod]
    public async Task CreateRoomAsync_ExceptionMessageMentionsLiveKitConfiguration()
    {
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.CreateRoomAsync(Guid.NewGuid(), 10));

        Assert.IsTrue(ex.Message.Contains("LiveKit"), "Exception message should mention LiveKit.");
        Assert.IsTrue(ex.Message.Contains("appsettings.json") || ex.Message.Contains("Chat:LiveKit"),
            "Exception message should reference configuration.");
    }

    // ══════════════════════════════════════════════════════════════
    //  GenerateToken Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void GenerateToken_ThrowsInvalidOperationException()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            _service.GenerateToken("room-1", "user-1", "User One"));
    }

    [TestMethod]
    public void GenerateToken_ExceptionMessageMentionsLiveKit()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            _service.GenerateToken("room-1", "user-1", "User One"));

        Assert.IsTrue(ex.Message.Contains("LiveKit"), "Exception message should mention LiveKit.");
    }

    // ══════════════════════════════════════════════════════════════
    //  DeleteRoomAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task DeleteRoomAsync_CompletesSuccessfully()
    {
        // Should not throw — graceful no-op when LiveKit not configured
        await _service.DeleteRoomAsync("room-1");
    }

    [TestMethod]
    public async Task DeleteRoomAsync_WithEmptyRoomName_CompletesSuccessfully()
    {
        await _service.DeleteRoomAsync(string.Empty);
    }

    // ══════════════════════════════════════════════════════════════
    //  GetRoomParticipantsAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetRoomParticipantsAsync_ReturnsEmptyList()
    {
        var result = await _service.GetRoomParticipantsAsync("room-1");

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetRoomParticipantsAsync_WithEmptyRoomName_ReturnsEmptyList()
    {
        var result = await _service.GetRoomParticipantsAsync(string.Empty);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    // ══════════════════════════════════════════════════════════════
    //  Logging Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateRoomAsync_LogsWarning()
    {
        var loggerMock = new TestLogger<NullLiveKitService>();
        var service = new NullLiveKitService(loggerMock);

        try { await service.CreateRoomAsync(Guid.NewGuid(), 10); }
        catch (InvalidOperationException) { }

        Assert.IsTrue(loggerMock.LogEntries.Any(e => e.LogLevel == LogLevel.Warning),
            "Should log a warning when CreateRoomAsync is called.");
    }

    [TestMethod]
    public void GenerateToken_LogsWarning()
    {
        var loggerMock = new TestLogger<NullLiveKitService>();
        var service = new NullLiveKitService(loggerMock);

        try { service.GenerateToken("room", "user", "name"); }
        catch (InvalidOperationException) { }

        Assert.IsTrue(loggerMock.LogEntries.Any(e => e.LogLevel == LogLevel.Warning),
            "Should log a warning when GenerateToken is called.");
    }
}

/// <summary>
/// Simple test logger that captures log entries for assertion.
/// </summary>
internal sealed class TestLogger<T> : ILogger<T>
{
    public List<LogEntry> LogEntries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogEntries.Add(new LogEntry { LogLevel = logLevel, Message = formatter(state, exception), Exception = exception });
    }

    internal sealed class LogEntry
    {
        public LogLevel LogLevel { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }
}
