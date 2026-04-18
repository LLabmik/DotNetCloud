using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Services;
using DotNetCloud.Modules.Music.UI;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class MusicPlaybackStateTests
{
    private Mock<IPlaybackService> _playbackMock = null!;
    private Mock<IEqPresetService> _eqPresetMock = null!;
    private MusicPlaybackState _state = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        _playbackMock = new Mock<IPlaybackService>();
        _eqPresetMock = new Mock<IEqPresetService>();
        _state = new MusicPlaybackState(_playbackMock.Object, _eqPresetMock.Object);
        _caller = TestHelpers.CreateCaller();
    }

    // ─── Helper to create a test TrackDto ──────────────────

    private static TrackDto MakeTrack(
        string title = "Test Track",
        Guid? id = null,
        Guid? albumId = null,
        TimeSpan? duration = null)
    {
        return new TrackDto
        {
            Id = id ?? Guid.NewGuid(),
            FileNodeId = Guid.NewGuid(),
            Title = title,
            Duration = duration ?? TimeSpan.FromSeconds(200),
            MimeType = "audio/flac",
            ArtistId = Guid.NewGuid(),
            ArtistName = "Test Artist",
            AlbumId = albumId,
            AlbumTitle = albumId is not null ? "Test Album" : null,
            CreatedAt = DateTime.UtcNow
        };
    }

    private List<TrackDto> MakeTracks(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => MakeTrack($"Track {i}"))
            .ToList();
    }

    // ═══════════════════════════════════════════════════════
    //  PlayTrack
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void PlayTrack_SetsNowPlayingAndIsPlaying()
    {
        var track = MakeTrack();

        _state.PlayTrack(track);

        Assert.AreEqual(track, _state.NowPlaying);
        Assert.IsTrue(_state.IsPlaying);
        Assert.AreEqual(TimeSpan.Zero, _state.PlaybackPosition);
    }

    [TestMethod]
    public void PlayTrack_AddsToQueueIfNotPresent()
    {
        var track = MakeTrack();

        _state.PlayTrack(track);

        Assert.AreEqual(1, _state.Queue.Count);
        Assert.AreEqual(track.Id, _state.Queue[0].Id);
        Assert.AreEqual(0, _state.QueueIndex);
    }

    [TestMethod]
    public void PlayTrack_DoesNotDuplicateInQueue()
    {
        var track = MakeTrack();

        _state.PlayTrack(track);
        _state.PlayTrack(track);

        Assert.AreEqual(1, _state.Queue.Count);
        Assert.AreEqual(0, _state.QueueIndex);
    }

    [TestMethod]
    public void PlayTrack_SetsQueueIndexToExistingTrack()
    {
        var track1 = MakeTrack("Track 1");
        var track2 = MakeTrack("Track 2");

        _state.PlayTrack(track1);
        _state.PlayTrack(track2);
        _state.PlayTrack(track1); // re-select first

        Assert.AreEqual(2, _state.Queue.Count);
        Assert.AreEqual(0, _state.QueueIndex); // back to index 0
    }

    [TestMethod]
    public void PlayTrack_RaisesOnChange()
    {
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.PlayTrack(MakeTrack());

        Assert.IsTrue(raised);
    }

    [TestMethod]
    public void PlayTrack_RaisesOnTrackChanged()
    {
        TrackDto? changedTrack = null;
        _state.OnTrackChanged += t => changedTrack = t;
        var track = MakeTrack();

        _state.PlayTrack(track);

        Assert.AreEqual(track, changedTrack);
    }

    [TestMethod]
    public void PlayTrack_ResetsPositionToZero()
    {
        var track1 = MakeTrack("Track 1");
        var track2 = MakeTrack("Track 2");
        _state.PlayTrack(track1);
        _state.UpdatePosition(TimeSpan.FromSeconds(90));

        _state.PlayTrack(track2);

        Assert.AreEqual(TimeSpan.Zero, _state.PlaybackPosition);
    }

    // ═══════════════════════════════════════════════════════
    //  PlayQueue
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void PlayQueue_ReplacesQueueAndPlaysFirst()
    {
        var tracks = MakeTracks(3);

        _state.PlayQueue(tracks);

        Assert.AreEqual(3, _state.Queue.Count);
        Assert.AreEqual(0, _state.QueueIndex);
        Assert.AreEqual(tracks[0], _state.NowPlaying);
        Assert.IsTrue(_state.IsPlaying);
    }

    [TestMethod]
    public void PlayQueue_ClearsExistingQueue()
    {
        _state.PlayTrack(MakeTrack("Old"));
        Assert.AreEqual(1, _state.Queue.Count);

        var tracks = MakeTracks(2);
        _state.PlayQueue(tracks);

        Assert.AreEqual(2, _state.Queue.Count);
        Assert.AreEqual(tracks[0].Title, _state.Queue[0].Title);
    }

    [TestMethod]
    public void PlayQueue_EmptyList_DoesNothing()
    {
        _state.PlayTrack(MakeTrack("Existing"));
        var original = _state.NowPlaying;

        _state.PlayQueue([]);

        Assert.AreEqual(original, _state.NowPlaying);
    }

    [TestMethod]
    public void PlayQueue_RaisesOnTrackChanged()
    {
        TrackDto? changedTrack = null;
        _state.OnTrackChanged += t => changedTrack = t;
        var tracks = MakeTracks(3);

        _state.PlayQueue(tracks);

        Assert.AreEqual(tracks[0], changedTrack);
    }

    // ═══════════════════════════════════════════════════════
    //  TogglePlayPause
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void TogglePlayPause_TogglesState()
    {
        _state.PlayTrack(MakeTrack());
        Assert.IsTrue(_state.IsPlaying);

        _state.TogglePlayPause();
        Assert.IsFalse(_state.IsPlaying);

        _state.TogglePlayPause();
        Assert.IsTrue(_state.IsPlaying);
    }

    [TestMethod]
    public void TogglePlayPause_RaisesOnChange()
    {
        _state.PlayTrack(MakeTrack());
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.TogglePlayPause();

        Assert.IsTrue(raised);
    }

    // ═══════════════════════════════════════════════════════
    //  SetPlaying
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void SetPlaying_SetsState()
    {
        _state.SetPlaying(true);
        Assert.IsTrue(_state.IsPlaying);

        _state.SetPlaying(false);
        Assert.IsFalse(_state.IsPlaying);
    }

    [TestMethod]
    public void SetPlaying_SameValue_DoesNotRaiseOnChange()
    {
        _state.PlayTrack(MakeTrack()); // IsPlaying = true
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.SetPlaying(true); // same value

        Assert.IsFalse(raised);
    }

    // ═══════════════════════════════════════════════════════
    //  PlayNext
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void PlayNext_AdvancesToNextTrack()
    {
        var tracks = MakeTracks(3);
        _state.PlayQueue(tracks);

        var result = _state.PlayNext();

        Assert.AreEqual(tracks[1], result);
        Assert.AreEqual(tracks[1], _state.NowPlaying);
        Assert.AreEqual(1, _state.QueueIndex);
    }

    [TestMethod]
    public void PlayNext_EmptyQueue_ReturnsNull()
    {
        var result = _state.PlayNext();

        Assert.IsNull(result);
    }

    [TestMethod]
    public void PlayNext_EndOfQueue_RepeatOff_StopsPlayback()
    {
        var tracks = MakeTracks(2);
        _state.PlayQueue(tracks);
        _state.PlayNext(); // at index 1 (last)

        var result = _state.PlayNext(); // past end

        Assert.IsNull(result);
        Assert.IsFalse(_state.IsPlaying);
    }

    [TestMethod]
    public void PlayNext_EndOfQueue_RepeatAll_WrapsToStart()
    {
        var tracks = MakeTracks(2);
        _state.PlayQueue(tracks);
        _state.CycleRepeat(); // Off → All
        _state.PlayNext(); // at index 1

        var result = _state.PlayNext(); // should wrap

        Assert.AreEqual(tracks[0], result);
        Assert.AreEqual(0, _state.QueueIndex);
        Assert.IsTrue(_state.IsPlaying);
    }

    [TestMethod]
    public void PlayNext_RepeatOne_RestartsCurrentTrack()
    {
        var tracks = MakeTracks(2);
        _state.PlayQueue(tracks);
        _state.CycleRepeat(); // Off → All
        _state.CycleRepeat(); // All → One

        var result = _state.PlayNext();

        Assert.AreEqual(tracks[0], result);
        Assert.AreEqual(TimeSpan.Zero, _state.PlaybackPosition);
    }

    [TestMethod]
    public void PlayNext_RepeatOne_RaisesOnTrackChanged()
    {
        var tracks = MakeTracks(2);
        _state.PlayQueue(tracks);
        _state.CycleRepeat(); // Off → All
        _state.CycleRepeat(); // All → One
        TrackDto? changedTrack = null;
        _state.OnTrackChanged += t => changedTrack = t;

        _state.PlayNext();

        Assert.AreEqual(tracks[0], changedTrack);
    }

    [TestMethod]
    public void PlayNext_Shuffle_PicksRandomTrack()
    {
        var tracks = MakeTracks(20); // large enough to virtually guarantee a non-sequential pick
        _state.PlayQueue(tracks);
        _state.ToggleShuffle();

        // Play next many times and collect indices
        var indices = new HashSet<int>();
        for (int i = 0; i < 50; i++)
        {
            _state.PlayNext();
            indices.Add(_state.QueueIndex);
        }

        // With 20 tracks and 50 iterations, we should see more than 1 unique index
        Assert.IsTrue(indices.Count > 1, "Shuffle should produce varied indices");
    }

    // ═══════════════════════════════════════════════════════
    //  PlayPrevious
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void PlayPrevious_GoesToPreviousTrack()
    {
        var tracks = MakeTracks(3);
        _state.PlayQueue(tracks);
        _state.PlayNext(); // index 1

        var result = _state.PlayPrevious();

        Assert.AreEqual(tracks[0], result);
        Assert.AreEqual(0, _state.QueueIndex);
    }

    [TestMethod]
    public void PlayPrevious_PastThreeSeconds_RestartsCurrentTrack()
    {
        var tracks = MakeTracks(3);
        _state.PlayQueue(tracks);
        _state.PlayNext(); // index 1
        _state.UpdatePosition(TimeSpan.FromSeconds(5)); // past 3s threshold

        var result = _state.PlayPrevious();

        Assert.AreEqual(tracks[1], result); // stays on same track
        Assert.AreEqual(1, _state.QueueIndex);
        Assert.AreEqual(TimeSpan.Zero, _state.PlaybackPosition);
    }

    [TestMethod]
    public void PlayPrevious_AtFirstTrack_StaysAtZero()
    {
        var tracks = MakeTracks(3);
        _state.PlayQueue(tracks);

        var result = _state.PlayPrevious();

        Assert.AreEqual(tracks[0], result);
        Assert.AreEqual(0, _state.QueueIndex);
    }

    [TestMethod]
    public void PlayPrevious_EmptyQueue_ReturnsNull()
    {
        var result = _state.PlayPrevious();

        Assert.IsNull(result);
    }

    [TestMethod]
    public void PlayPrevious_RaisesOnTrackChanged()
    {
        var tracks = MakeTracks(3);
        _state.PlayQueue(tracks);
        _state.PlayNext(); // index 1
        TrackDto? changedTrack = null;
        _state.OnTrackChanged += t => changedTrack = t;

        _state.PlayPrevious();

        Assert.IsNotNull(changedTrack);
    }

    // ═══════════════════════════════════════════════════════
    //  Stop
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void Stop_ClearsPlaybackState()
    {
        _state.PlayTrack(MakeTrack());
        _state.ToggleQueue();
        _state.ToggleEqualizer();

        _state.Stop();

        Assert.IsNull(_state.NowPlaying);
        Assert.IsFalse(_state.IsPlaying);
        Assert.AreEqual(TimeSpan.Zero, _state.PlaybackPosition);
        Assert.IsFalse(_state.ShowQueue);
        Assert.IsFalse(_state.ShowEqualizer);
        Assert.IsFalse(_state.ShowSavePresetDialog);
    }

    [TestMethod]
    public void Stop_RaisesOnChange()
    {
        _state.PlayTrack(MakeTrack());
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.Stop();

        Assert.IsTrue(raised);
    }

    // ═══════════════════════════════════════════════════════
    //  Position / Seek
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void UpdatePosition_SetsPosition()
    {
        var pos = TimeSpan.FromSeconds(42);

        _state.UpdatePosition(pos);

        Assert.AreEqual(pos, _state.PlaybackPosition);
    }

    [TestMethod]
    public void UpdatePosition_DoesNotRaiseOnChange()
    {
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.UpdatePosition(TimeSpan.FromSeconds(10));

        Assert.IsFalse(raised);
    }

    [TestMethod]
    public void Seek_SetsPositionAndRaisesOnChange()
    {
        var raised = false;
        _state.OnChange += () => raised = true;
        var pos = TimeSpan.FromSeconds(60);

        _state.Seek(pos);

        Assert.AreEqual(pos, _state.PlaybackPosition);
        Assert.IsTrue(raised);
    }

    // ═══════════════════════════════════════════════════════
    //  UpdateDurationFromMetadata
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void UpdateDurationFromMetadata_UpdatesWhenOriginalIsZero()
    {
        var track = MakeTrack(duration: TimeSpan.Zero);
        _state.PlayTrack(track);

        _state.UpdateDurationFromMetadata(180.5);

        Assert.IsNotNull(_state.NowPlaying);
        Assert.AreEqual(180.5, _state.NowPlaying.Duration.TotalSeconds, 0.01);
    }

    [TestMethod]
    public void UpdateDurationFromMetadata_DoesNotOverwriteExistingDuration()
    {
        var track = MakeTrack(duration: TimeSpan.FromSeconds(200));
        _state.PlayTrack(track);

        _state.UpdateDurationFromMetadata(180.5);

        Assert.AreEqual(200, _state.NowPlaying!.Duration.TotalSeconds, 0.01);
    }

    [TestMethod]
    public void UpdateDurationFromMetadata_NoTrackPlaying_NoException()
    {
        _state.UpdateDurationFromMetadata(180.5); // should not throw
    }

    // ═══════════════════════════════════════════════════════
    //  UpdateNowPlaying
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void UpdateNowPlaying_UpdatesMetadataWithoutOnTrackChanged()
    {
        var track = MakeTrack(title: "Original");
        _state.PlayTrack(track);
        var trackChangedFired = false;
        _state.OnTrackChanged += _ => trackChangedFired = true;

        var updated = track with { IsStarred = true };
        _state.UpdateNowPlaying(updated);

        Assert.IsTrue(_state.NowPlaying!.IsStarred);
        Assert.IsFalse(trackChangedFired);
    }

    [TestMethod]
    public void UpdateNowPlaying_DifferentId_NoEffect()
    {
        var track = MakeTrack(title: "Playing");
        _state.PlayTrack(track);

        var unrelated = MakeTrack(title: "Different");
        _state.UpdateNowPlaying(unrelated);

        Assert.AreEqual("Playing", _state.NowPlaying!.Title);
    }

    [TestMethod]
    public void UpdateNowPlaying_RaisesOnChange()
    {
        var track = MakeTrack();
        _state.PlayTrack(track);
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.UpdateNowPlaying(track with { IsStarred = true });

        Assert.IsTrue(raised);
    }

    // ═══════════════════════════════════════════════════════
    //  Volume / Mute
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void SetVolume_ClampsToRange()
    {
        _state.SetVolume(150);
        Assert.AreEqual(100, _state.Volume);

        _state.SetVolume(-10);
        Assert.AreEqual(0, _state.Volume);

        _state.SetVolume(50);
        Assert.AreEqual(50, _state.Volume);
    }

    [TestMethod]
    public void SetVolume_RaisesOnChange()
    {
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.SetVolume(42);

        Assert.IsTrue(raised);
    }

    [TestMethod]
    public void ToggleMute_TogglesMutedState()
    {
        Assert.IsFalse(_state.Muted);

        _state.ToggleMute();
        Assert.IsTrue(_state.Muted);

        _state.ToggleMute();
        Assert.IsFalse(_state.Muted);
    }

    [TestMethod]
    public void DefaultVolume_Is80()
    {
        Assert.AreEqual(80, _state.Volume);
    }

    // ═══════════════════════════════════════════════════════
    //  Shuffle / Repeat
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void ToggleShuffle_TogglesState()
    {
        Assert.IsFalse(_state.Shuffle);

        _state.ToggleShuffle();
        Assert.IsTrue(_state.Shuffle);

        _state.ToggleShuffle();
        Assert.IsFalse(_state.Shuffle);
    }

    [TestMethod]
    public void CycleRepeat_CyclesThroughModes()
    {
        Assert.AreEqual(RepeatMode.Off, _state.Repeat);

        _state.CycleRepeat();
        Assert.AreEqual(RepeatMode.All, _state.Repeat);

        _state.CycleRepeat();
        Assert.AreEqual(RepeatMode.One, _state.Repeat);

        _state.CycleRepeat();
        Assert.AreEqual(RepeatMode.Off, _state.Repeat);
    }

    [TestMethod]
    public void ToggleShuffle_RaisesOnChange()
    {
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.ToggleShuffle();

        Assert.IsTrue(raised);
    }

    [TestMethod]
    public void CycleRepeat_RaisesOnChange()
    {
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.CycleRepeat();

        Assert.IsTrue(raised);
    }

    // ═══════════════════════════════════════════════════════
    //  Queue management
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void AddToQueue_AppendsTrack()
    {
        _state.PlayTrack(MakeTrack("First"));

        var extra = MakeTrack("Added");
        _state.AddToQueue(extra);

        Assert.AreEqual(2, _state.Queue.Count);
        Assert.AreEqual("Added", _state.Queue[1].Title);
    }

    [TestMethod]
    public void AddToQueue_RaisesOnChange()
    {
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.AddToQueue(MakeTrack());

        Assert.IsTrue(raised);
    }

    [TestMethod]
    public void RemoveFromQueue_RemovesTrack()
    {
        var tracks = MakeTracks(3);
        _state.PlayQueue(tracks);

        _state.RemoveFromQueue(1);

        Assert.AreEqual(2, _state.Queue.Count);
        Assert.AreEqual(tracks[2].Id, _state.Queue[1].Id);
    }

    [TestMethod]
    public void RemoveFromQueue_BeforeCurrentIndex_AdjustsIndex()
    {
        var tracks = MakeTracks(3);
        _state.PlayQueue(tracks);
        _state.PlayNext(); // now at index 1

        _state.RemoveFromQueue(0); // remove before current

        Assert.AreEqual(0, _state.QueueIndex); // adjusted down
    }

    [TestMethod]
    public void RemoveFromQueue_InvalidIndex_DoesNothing()
    {
        var tracks = MakeTracks(2);
        _state.PlayQueue(tracks);

        _state.RemoveFromQueue(-1);
        _state.RemoveFromQueue(10);

        Assert.AreEqual(2, _state.Queue.Count);
    }

    [TestMethod]
    public void RemoveFromQueue_RaisesOnChange()
    {
        _state.PlayQueue(MakeTracks(2));
        var raised = false;
        _state.OnChange += () => raised = true;

        _state.RemoveFromQueue(0);

        Assert.IsTrue(raised);
    }

    // ═══════════════════════════════════════════════════════
    //  Equalizer
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void SetEqBand_SetsGain()
    {
        _state.SetEqBand(0, 6.0);

        Assert.AreEqual(6.0, _state.EqBands[0]);
    }

    [TestMethod]
    public void SetEqBand_InvalidIndex_DoesNothing()
    {
        _state.SetEqBand(-1, 6.0);
        _state.SetEqBand(10, 6.0);

        // All bands should still be 0
        Assert.IsTrue(_state.EqBands.All(b => b == 0));
    }

    [TestMethod]
    public void ResetEqBands_SetsAllToZero()
    {
        _state.SetEqBand(0, 6.0);
        _state.SetEqBand(5, -3.0);

        _state.ResetEqBands();

        Assert.IsTrue(_state.EqBands.All(b => b == 0));
        Assert.IsNull(_state.ActivePresetId);
    }

    [TestMethod]
    public void ApplyPreset_SetsBandsAndActivePresetId()
    {
        var presetId = Guid.NewGuid();
        var bands = new Dictionary<string, double>
        {
            ["31"] = 3.0, ["63"] = 2.0, ["125"] = 1.0, ["250"] = 0.5, ["500"] = 0,
            ["1K"] = -0.5, ["2K"] = -1.0, ["4K"] = -2.0, ["8K"] = -3.0, ["16K"] = -4.0
        };
        var preset = new EqPresetDto
        {
            Id = presetId,
            Name = "Rock",
            IsBuiltIn = true,
            Bands = bands
        };

        _state.ApplyPreset(preset);

        Assert.AreEqual(presetId, _state.ActivePresetId);
        Assert.AreEqual(3.0, _state.EqBands[0]);
        Assert.AreEqual(-4.0, _state.EqBands[9]);
    }

    [TestMethod]
    public void ApplyPreset_RaisesOnChange()
    {
        var raised = false;
        _state.OnChange += () => raised = true;
        var preset = new EqPresetDto
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Bands = Enumerable.Range(0, 10).ToDictionary(i => MusicPlaybackState.BandLabels[i], _ => 0.0)
        };

        _state.ApplyPreset(preset);

        Assert.IsTrue(raised);
    }

    [TestMethod]
    public async Task LoadEqPresetsAsync_LoadsFromService()
    {
        var presets = new List<EqPresetDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Flat", Bands = new Dictionary<string, double>() },
            new() { Id = Guid.NewGuid(), Name = "Rock", Bands = new Dictionary<string, double>() }
        };
        _eqPresetMock.Setup(s => s.ListPresetsAsync(_caller, default))
            .ReturnsAsync(presets);

        await _state.LoadEqPresetsAsync(_caller);

        Assert.AreEqual(2, _state.EqPresets.Count);
        Assert.IsTrue(_state.EqPresetsLoaded);
    }

    [TestMethod]
    public async Task LoadEqPresetsAsync_CachesAfterFirstCall()
    {
        _eqPresetMock.Setup(s => s.ListPresetsAsync(_caller, default))
            .ReturnsAsync(Array.Empty<EqPresetDto>());

        await _state.LoadEqPresetsAsync(_caller);
        await _state.LoadEqPresetsAsync(_caller);

        _eqPresetMock.Verify(s => s.ListPresetsAsync(_caller, default), Times.Once);
    }

    [TestMethod]
    public async Task LoadEqPresetsAsync_SwallowsException()
    {
        _eqPresetMock.Setup(s => s.ListPresetsAsync(_caller, default))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await _state.LoadEqPresetsAsync(_caller); // should not throw

        Assert.IsFalse(_state.EqPresetsLoaded);
    }

    [TestMethod]
    public async Task SavePresetAsync_CreatesAndCaches()
    {
        var createdPreset = new EqPresetDto
        {
            Id = Guid.NewGuid(),
            Name = "My Preset",
            Bands = new Dictionary<string, double>()
        };
        _eqPresetMock.Setup(s => s.CreatePresetAsync(It.IsAny<SaveEqPresetDto>(), _caller, default))
            .ReturnsAsync(createdPreset);

        _state.SetEqBand(0, 5.0);
        var result = await _state.SavePresetAsync("My Preset", _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("My Preset", result.Name);
        Assert.AreEqual(createdPreset.Id, _state.ActivePresetId);
        Assert.IsFalse(_state.ShowSavePresetDialog);
        Assert.AreEqual(1, _state.EqPresets.Count);
    }

    [TestMethod]
    public async Task SavePresetAsync_TrimsPretName()
    {
        var createdPreset = new EqPresetDto
        {
            Id = Guid.NewGuid(),
            Name = "Trimmed",
            Bands = new Dictionary<string, double>()
        };
        _eqPresetMock.Setup(s => s.CreatePresetAsync(
                It.Is<SaveEqPresetDto>(d => d.Name == "Trimmed"),
                _caller, default))
            .ReturnsAsync(createdPreset);

        await _state.SavePresetAsync("  Trimmed  ", _caller);

        _eqPresetMock.Verify(s => s.CreatePresetAsync(
            It.Is<SaveEqPresetDto>(d => d.Name == "Trimmed"),
            _caller, default), Times.Once);
    }

    // ═══════════════════════════════════════════════════════
    //  Panel toggles
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void ToggleQueue_TogglesVisibility()
    {
        Assert.IsFalse(_state.ShowQueue);

        _state.ToggleQueue();
        Assert.IsTrue(_state.ShowQueue);

        _state.ToggleQueue();
        Assert.IsFalse(_state.ShowQueue);
    }

    [TestMethod]
    public void ToggleEqualizer_TogglesVisibility()
    {
        Assert.IsFalse(_state.ShowEqualizer);

        _state.ToggleEqualizer();
        Assert.IsTrue(_state.ShowEqualizer);

        _state.ToggleEqualizer();
        Assert.IsFalse(_state.ShowEqualizer);
    }

    [TestMethod]
    public void ToggleSavePresetDialog_TogglesVisibility()
    {
        Assert.IsFalse(_state.ShowSavePresetDialog);

        _state.ToggleSavePresetDialog();
        Assert.IsTrue(_state.ShowSavePresetDialog);

        _state.ToggleSavePresetDialog();
        Assert.IsFalse(_state.ShowSavePresetDialog);
    }

    // ═══════════════════════════════════════════════════════
    //  Starring (async delegates)
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public async Task ToggleStarNowPlayingAsync_TogglesStarredFlag()
    {
        var track = MakeTrack();
        Assert.IsFalse(track.IsStarred);
        _state.PlayTrack(track);

        await _state.ToggleStarNowPlayingAsync(_caller);

        Assert.IsTrue(_state.NowPlaying!.IsStarred);
        _playbackMock.Verify(
            s => s.ToggleStarAsync(track.Id, StarredItemType.Track, _caller, default),
            Times.Once);
    }

    [TestMethod]
    public async Task ToggleStarNowPlayingAsync_NoTrack_DoesNothing()
    {
        await _state.ToggleStarNowPlayingAsync(_caller); // should not throw

        _playbackMock.Verify(
            s => s.ToggleStarAsync(It.IsAny<Guid>(), It.IsAny<StarredItemType>(), It.IsAny<CallerContext>(), default),
            Times.Never);
    }

    [TestMethod]
    public async Task RecordPlayAsync_DelegatesToService()
    {
        var track = MakeTrack(duration: TimeSpan.FromSeconds(210));
        _state.PlayTrack(track);

        await _state.RecordPlayAsync(_caller);

        _playbackMock.Verify(
            s => s.RecordPlayAsync(track.Id, 210, _caller, default),
            Times.Once);
    }

    [TestMethod]
    public async Task RecordPlayAsync_NoTrack_DoesNothing()
    {
        await _state.RecordPlayAsync(_caller); // should not throw

        _playbackMock.Verify(
            s => s.RecordPlayAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CallerContext>(), default),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════
    //  Progress helpers
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void GetProgressPercent_ReturnsCorrectPercentage()
    {
        var track = MakeTrack(duration: TimeSpan.FromSeconds(200));
        _state.PlayTrack(track);
        _state.UpdatePosition(TimeSpan.FromSeconds(50));

        var pct = _state.GetProgressPercent();

        Assert.AreEqual(25.0, pct, 0.01);
    }

    [TestMethod]
    public void GetProgressPercent_NoTrack_ReturnsZero()
    {
        Assert.AreEqual(0, _state.GetProgressPercent());
    }

    [TestMethod]
    public void GetProgressPercent_ZeroDuration_ReturnsZero()
    {
        var track = MakeTrack(duration: TimeSpan.Zero);
        _state.PlayTrack(track);
        _state.UpdatePosition(TimeSpan.FromSeconds(10));

        Assert.AreEqual(0, _state.GetProgressPercent());
    }

    // ═══════════════════════════════════════════════════════
    //  Static helpers
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void GetAudioUrl_ReturnsCorrectPath()
    {
        var fileNodeId = Guid.NewGuid();
        var track = MakeTrack();
        var trackWithFileNode = track with { FileNodeId = fileNodeId };

        var url = MusicPlaybackState.GetAudioUrl(trackWithFileNode);

        Assert.AreEqual($"/api/v1/files/{fileNodeId}/content", url);
    }

    [TestMethod]
    public void GetAlbumArtUrl_ReturnsCorrectPath()
    {
        var albumId = Guid.NewGuid();

        var url = MusicPlaybackState.GetAlbumArtUrl(albumId);

        Assert.AreEqual($"/api/v1/music/albums/{albumId}/cover", url);
    }

    [TestMethod]
    [DataRow(0, 0, "0:00")]
    [DataRow(0, 30, "0:30")]
    [DataRow(3, 5, "3:05")]
    [DataRow(59, 59, "59:59")]
    public void FormatDuration_MinutesSeconds(int minutes, int seconds, string expected)
    {
        var duration = new TimeSpan(0, minutes, seconds);

        var result = MusicPlaybackState.FormatDuration(duration);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(1, 0, 0, "1:00:00")]
    [DataRow(1, 30, 45, "1:30:45")]
    [DataRow(2, 5, 3, "2:05:03")]
    public void FormatDuration_HoursMinutesSeconds(int hours, int minutes, int seconds, string expected)
    {
        var duration = new TimeSpan(hours, minutes, seconds);

        var result = MusicPlaybackState.FormatDuration(duration);

        Assert.AreEqual(expected, result);
    }

    // ═══════════════════════════════════════════════════════
    //  Default state
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void InitialState_IsCorrect()
    {
        Assert.IsNull(_state.NowPlaying);
        Assert.IsFalse(_state.IsPlaying);
        Assert.AreEqual(TimeSpan.Zero, _state.PlaybackPosition);
        Assert.AreEqual(0, _state.Queue.Count);
        Assert.AreEqual(-1, _state.QueueIndex);
        Assert.IsFalse(_state.Shuffle);
        Assert.AreEqual(RepeatMode.Off, _state.Repeat);
        Assert.AreEqual(80, _state.Volume);
        Assert.IsFalse(_state.Muted);
        Assert.AreEqual(10, _state.EqBands.Length);
        Assert.IsTrue(_state.EqBands.All(b => b == 0));
        Assert.IsNull(_state.ActivePresetId);
        Assert.IsFalse(_state.EqPresetsLoaded);
        Assert.IsFalse(_state.ShowQueue);
        Assert.IsFalse(_state.ShowEqualizer);
        Assert.IsFalse(_state.ShowSavePresetDialog);
    }

    [TestMethod]
    public void BandLabels_HasTenEntries()
    {
        Assert.AreEqual(10, MusicPlaybackState.BandLabels.Length);
        Assert.AreEqual("31", MusicPlaybackState.BandLabels[0]);
        Assert.AreEqual("16K", MusicPlaybackState.BandLabels[9]);
    }

    // ═══════════════════════════════════════════════════════
    //  Complex scenarios / integration
    // ═══════════════════════════════════════════════════════

    [TestMethod]
    public void FullPlaybackCycle_PlayPauseNextPreviousStop()
    {
        var tracks = MakeTracks(3);

        // Start playing
        _state.PlayQueue(tracks);
        Assert.IsTrue(_state.IsPlaying);
        Assert.AreEqual(tracks[0], _state.NowPlaying);

        // Pause
        _state.TogglePlayPause();
        Assert.IsFalse(_state.IsPlaying);

        // Resume
        _state.TogglePlayPause();
        Assert.IsTrue(_state.IsPlaying);

        // Next
        _state.PlayNext();
        Assert.AreEqual(tracks[1], _state.NowPlaying);

        // Next again
        _state.PlayNext();
        Assert.AreEqual(tracks[2], _state.NowPlaying);

        // Previous
        _state.PlayPrevious();
        Assert.AreEqual(tracks[1], _state.NowPlaying);

        // Stop
        _state.Stop();
        Assert.IsNull(_state.NowPlaying);
        Assert.IsFalse(_state.IsPlaying);
    }

    [TestMethod]
    public void PlayTrack_WhileQueueActive_KeepsQueueIntact()
    {
        var tracks = MakeTracks(5);
        _state.PlayQueue(tracks);
        _state.PlayNext(); // at index 1

        // Play a track that's already in queue at index 3
        _state.PlayTrack(tracks[3]);

        Assert.AreEqual(5, _state.Queue.Count); // queue unchanged
        Assert.AreEqual(3, _state.QueueIndex);
        Assert.AreEqual(tracks[3], _state.NowPlaying);
    }

    [TestMethod]
    public void PlayTrack_NewTrackWhileQueueActive_AppendsToQueue()
    {
        var tracks = MakeTracks(3);
        _state.PlayQueue(tracks);

        var newTrack = MakeTrack("Brand New");
        _state.PlayTrack(newTrack);

        Assert.AreEqual(4, _state.Queue.Count);
        Assert.AreEqual(3, _state.QueueIndex);
        Assert.AreEqual(newTrack, _state.NowPlaying);
    }

    [TestMethod]
    public void RepeatAll_PlaysEntireQueueThenLoops()
    {
        var tracks = MakeTracks(2);
        _state.PlayQueue(tracks);
        _state.CycleRepeat(); // Off → All

        _state.PlayNext(); // track 2
        Assert.AreEqual(tracks[1], _state.NowPlaying);

        var looped = _state.PlayNext(); // should wrap to track 1
        Assert.AreEqual(tracks[0], looped);
        Assert.IsTrue(_state.IsPlaying);
    }

    [TestMethod]
    public void MultipleEventSubscribers_AllNotified()
    {
        var count = 0;
        _state.OnChange += () => count++;
        _state.OnChange += () => count++;

        _state.PlayTrack(MakeTrack());

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void RemoveFromQueue_AfterCurrentIndex_DoesNotChangeIndex()
    {
        var tracks = MakeTracks(4);
        _state.PlayQueue(tracks);
        Assert.AreEqual(0, _state.QueueIndex);

        _state.RemoveFromQueue(2); // remove track after current

        Assert.AreEqual(0, _state.QueueIndex); // unchanged
        Assert.AreEqual(3, _state.Queue.Count);
    }

    [TestMethod]
    public void SetVolume_EdgeCases()
    {
        _state.SetVolume(0);
        Assert.AreEqual(0, _state.Volume);

        _state.SetVolume(100);
        Assert.AreEqual(100, _state.Volume);

        _state.SetVolume(int.MaxValue);
        Assert.AreEqual(100, _state.Volume);

        _state.SetVolume(int.MinValue);
        Assert.AreEqual(0, _state.Volume);
    }

    [TestMethod]
    public void EqBand_BoundaryValues()
    {
        _state.SetEqBand(0, -12.0);
        Assert.AreEqual(-12.0, _state.EqBands[0]);

        _state.SetEqBand(9, 12.0);
        Assert.AreEqual(12.0, _state.EqBands[9]);
    }

    [TestMethod]
    public async Task ToggleStarNowPlaying_TwiceTogglesBackAndForth()
    {
        var track = MakeTrack();
        _state.PlayTrack(track);

        await _state.ToggleStarNowPlayingAsync(_caller);
        Assert.IsTrue(_state.NowPlaying!.IsStarred);

        await _state.ToggleStarNowPlayingAsync(_caller);
        Assert.IsFalse(_state.NowPlaying!.IsStarred);
    }
}
