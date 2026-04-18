using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class MusicStreamingServiceTests
{
    private MusicDbContext _db;
    private MusicStreamingService _service;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new MusicStreamingService(_db, NullLogger<MusicStreamingService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── GetTrackForStreaming ──────────────────────────────────────────

    [TestMethod]
    public async Task GetTrackForStreaming_ExistingTrack_ReturnsTrack()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        var result = await _service.GetTrackForStreamingAsync(track.Id, _caller.UserId);

        Assert.IsNotNull(result);
        Assert.AreEqual(track.Id, result.Id);
    }

    [TestMethod]
    public async Task GetTrackForStreaming_NonExistent_ReturnsNull()
    {
        var result = await _service.GetTrackForStreamingAsync(Guid.NewGuid(), _caller.UserId);
        Assert.IsNull(result);
    }

    // ─── GenerateStreamToken ──────────────────────────────────────────

    [TestMethod]
    public void GenerateStreamToken_ReturnsNonEmptyToken()
    {
        var token = _service.GenerateStreamToken(Guid.NewGuid(), _caller.UserId);

        Assert.IsNotNull(token);
        Assert.IsTrue(token.Length > 10);
    }

    [TestMethod]
    public void GenerateStreamToken_UniqueTokensPerCall()
    {
        var trackId = Guid.NewGuid();
        var token1 = _service.GenerateStreamToken(trackId, _caller.UserId);
        var token2 = _service.GenerateStreamToken(trackId, _caller.UserId);

        Assert.AreNotEqual(token1, token2);
    }

    // ─── ValidateStreamToken ──────────────────────────────────────────

    [TestMethod]
    public void ValidateStreamToken_ValidToken_ReturnsTrackIdAndUserId()
    {
        var trackId = Guid.NewGuid();
        var token = _service.GenerateStreamToken(trackId, _caller.UserId);

        var result = _service.ValidateStreamToken(token);

        Assert.IsNotNull(result);
        Assert.AreEqual(trackId, result.TrackId);
        Assert.AreEqual(_caller.UserId, result.UserId);
    }

    [TestMethod]
    public void ValidateStreamToken_InvalidToken_ReturnsNull()
    {
        var result = _service.ValidateStreamToken("invalid-token-string");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ValidateStreamToken_EmptyString_ReturnsNull()
    {
        var result = _service.ValidateStreamToken("");

        Assert.IsNull(result);
    }

    // ─── AcquireStreamSlot / ReleaseStreamSlot ────────────────────────

    [TestMethod]
    public void AcquireStreamSlot_FirstSlot_Succeeds()
    {
        _service.AcquireStreamSlot(_caller.UserId);

        Assert.AreEqual(1, _service.GetActiveStreamCount(_caller.UserId));
    }

    [TestMethod]
    public void AcquireStreamSlot_ExceedMaxSlots_Throws()
    {
        // Default max is typically 3
        _service.AcquireStreamSlot(_caller.UserId);
        _service.AcquireStreamSlot(_caller.UserId);
        _service.AcquireStreamSlot(_caller.UserId);

        Assert.ThrowsExactly<DotNetCloud.Core.Errors.BusinessRuleException>(
            () => _service.AcquireStreamSlot(_caller.UserId));
    }

    [TestMethod]
    public void ReleaseStreamSlot_FreesSlot()
    {
        _service.AcquireStreamSlot(_caller.UserId);
        _service.AcquireStreamSlot(_caller.UserId);
        _service.AcquireStreamSlot(_caller.UserId);

        _service.ReleaseStreamSlot(_caller.UserId);

        // Should not throw now
        _service.AcquireStreamSlot(_caller.UserId);
    }

    [TestMethod]
    public void ReleaseStreamSlot_NoSlots_DoesNotThrow()
    {
        // Should not throw
        _service.ReleaseStreamSlot(_caller.UserId);
    }

    [TestMethod]
    public void AcquireStreamSlot_DifferentUsers_IndependentSlots()
    {
        var user2 = Guid.NewGuid();

        // Fill user1 slots
        _service.AcquireStreamSlot(_caller.UserId);
        _service.AcquireStreamSlot(_caller.UserId);
        _service.AcquireStreamSlot(_caller.UserId);

        // User2 should still be able to acquire
        _service.AcquireStreamSlot(user2);
        Assert.AreEqual(1, _service.GetActiveStreamCount(user2));
    }

    // ─── ParseRangeHeader ─────────────────────────────────────────────

    [TestMethod]
    public void ParseRangeHeader_StartAndEnd_ParsesCorrectly()
    {
        var result = MusicStreamingService.ParseRangeHeader("bytes=0-1023", 10000);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Value.Start);
        Assert.AreEqual(1023, result.Value.End);
    }

    [TestMethod]
    public void ParseRangeHeader_StartOnly_ParsesCorrectly()
    {
        var result = MusicStreamingService.ParseRangeHeader("bytes=500-", 10000);

        Assert.IsNotNull(result);
        Assert.AreEqual(500, result.Value.Start);
        Assert.AreEqual(9999, result.Value.End);
    }

    [TestMethod]
    public void ParseRangeHeader_SuffixRange_ParsesCorrectly()
    {
        var result = MusicStreamingService.ParseRangeHeader("bytes=-500", 10000);

        Assert.IsNotNull(result);
        Assert.AreEqual(9500, result.Value.Start);
        Assert.AreEqual(9999, result.Value.End);
    }

    [TestMethod]
    public void ParseRangeHeader_Null_ReturnsNull()
    {
        var result = MusicStreamingService.ParseRangeHeader(null, 10000);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRangeHeader_InvalidFormat_ReturnsNull()
    {
        var result = MusicStreamingService.ParseRangeHeader("invalid", 10000);
        Assert.IsNull(result);
    }
}
