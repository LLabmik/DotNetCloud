using DotNetCloud.Client.Core.Conflict;
using DotNetCloud.Client.Core.LocalState;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DotNetCloud.Client.Core.Tests.Conflict;

[TestClass]
public class ConflictResolverTests
{
    private string _tempDir = null!;
    private Mock<ILocalStateDb> _stateDb = null!;
    private ConflictResolver _resolver = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);

        _stateDb = new Mock<ILocalStateDb>();
        _stateDb
            .Setup(db => db.SaveConflictRecordAsync(
                It.IsAny<string>(), It.IsAny<ConflictRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _resolver = new ConflictResolver(_stateDb.Object, NullLogger<ConflictResolver>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── ResolveAsync: fallback (conflict copy) ──────────────────────────────

    [TestMethod]
    public async Task ResolveAsync_MovesLocalFileToConflictCopy()
    {
        var localPath = Path.Combine(_tempDir, "report.docx");
        await File.WriteAllTextAsync(localPath, "original content");

        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
        });

        Assert.AreEqual(ConflictResolutionOutcome.ConflictCopyCreated, outcome);
        Assert.IsFalse(File.Exists(localPath), "Original path should be renamed.");
        var conflictFiles = Directory.GetFiles(_tempDir, "*conflict*");
        Assert.AreEqual(1, conflictFiles.Length);
    }

    [TestMethod]
    public async Task ResolveAsync_FiresConflictDetectedEvent()
    {
        var localPath = Path.Combine(_tempDir, "notes.txt");
        await File.WriteAllTextAsync(localPath, "content");

        ConflictDetectedEventArgs? eventArgs = null;
        _resolver.ConflictDetected += (_, args) => eventArgs = args;

        await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
        });

        Assert.IsNotNull(eventArgs);
        Assert.AreEqual(localPath, eventArgs.OriginalPath);
        StringAssert.Contains(eventArgs.ConflictCopyPath, "conflict");
    }

    [TestMethod]
    public async Task ResolveAsync_NonExistentFile_NoOp()
    {
        var localPath = Path.Combine(_tempDir, "missing.txt");

        // Should not throw
        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
        });

        // Non-existent file falls through all strategies and returns ConflictCopyCreated
        // (File.Exists check at top returns false → early return)
        Assert.AreEqual(ConflictResolutionOutcome.ConflictCopyCreated, outcome);
    }

    [TestMethod]
    public async Task ResolveAsync_Fallback_SavesConflictRecordToDb()
    {
        var localPath = Path.Combine(_tempDir, "report.docx");
        await File.WriteAllTextAsync(localPath, "original");
        const string dbPath = "/test/state.db";

        await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
            StateDatabasePath = dbPath,
        });

        _stateDb.Verify(db => db.SaveConflictRecordAsync(
            dbPath,
            It.Is<ConflictRecord>(r => r.OriginalPath == localPath && !r.AutoResolved),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Strategy 1: Identical hashes ───────────────────────────────────────

    [TestMethod]
    public async Task ResolveAsync_Strategy1_IdenticalHashes_ReturnsAutoResolvedIdentical()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "same content");

        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
            LocalContentHash = "abc123",
            RemoteContentHash = "ABC123", // Case-insensitive comparison
        });

        Assert.AreEqual(ConflictResolutionOutcome.AutoResolvedIdentical, outcome);
        Assert.IsTrue(File.Exists(localPath), "File should remain untouched.");
        Assert.AreEqual(0, Directory.GetFiles(_tempDir, "*conflict*").Length);
    }

    [TestMethod]
    public async Task ResolveAsync_Strategy1_IdenticalHashes_FiresAutoResolvedEvent()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "same content");

        ConflictAutoResolvedEventArgs? args = null;
        _resolver.AutoResolved += (_, a) => args = a;

        await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
            LocalContentHash = "deadbeef",
            RemoteContentHash = "DEADBEEF",
        });

        Assert.IsNotNull(args);
        Assert.AreEqual(localPath, args.LocalPath);
        StringAssert.Contains(args.Strategy, "Strategy 1");
    }

    // ── Strategy 2: Fast-forward (one side unchanged) ──────────────────────

    [TestMethod]
    public async Task ResolveAsync_Strategy2_LocalUnchanged_ReturnsServerWins()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "base content");

        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
            LocalContentHash = "basehash",
            BaseContentHash = "basehash",   // local == base → server wins
            RemoteContentHash = "newhash",
        });

        Assert.AreEqual(ConflictResolutionOutcome.AutoResolvedServerWins, outcome);
    }

    [TestMethod]
    public async Task ResolveAsync_Strategy2_ServerUnchanged_ReturnsLocalWins()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "local content");

        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
            LocalContentHash = "localnewhash",
            BaseContentHash = "basehash",
            RemoteContentHash = "basehash", // remote == base → local wins
        });

        Assert.AreEqual(ConflictResolutionOutcome.AutoResolvedLocalWins, outcome);
    }

    // ── Strategy 3: Three-way text merge ───────────────────────────────────

    [TestMethod]
    public async Task ResolveAsync_Strategy3_CleanTextMerge_WritesFile()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        // Base: two lines shared. Local adds a line at the end. Server adds a line at the start.
        const string baseContent = "line1\nline2\n";
        const string localContent = "line1\nline2\nlocal-addition\n";
        const string serverContent = "server-addition\nline1\nline2\n";

        await File.WriteAllTextAsync(localPath, localContent);

        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
            LocalContentHash = "l",
            RemoteContentHash = "r",
            BaseContent = baseContent,
            LocalContent = localContent,
            ServerContent = serverContent,
        });

        Assert.AreEqual(ConflictResolutionOutcome.AutoResolvedLocalWins, outcome);

        var written = await File.ReadAllTextAsync(localPath);
        StringAssert.Contains(written, "local-addition");
        StringAssert.Contains(written, "server-addition");
    }

    // ── Strategy 4: Newer-wins (timestamp heuristic) ──────────────────────

    [TestMethod]
    public async Task ResolveAsync_Strategy4_LocalIsNewer_ReturnsLocalWins()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "local");

        var now = DateTime.UtcNow;
        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            LocalModifiedAt = now,
            RemoteUpdatedAt = now.AddMinutes(-10), // local is 10 min newer
        });

        Assert.AreEqual(ConflictResolutionOutcome.AutoResolvedLocalWins, outcome);
    }

    [TestMethod]
    public async Task ResolveAsync_Strategy4_ServerIsNewer_ReturnsServerWins()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "local");

        var now = DateTime.UtcNow;
        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            LocalModifiedAt = now.AddMinutes(-10), // server is 10 min newer
            RemoteUpdatedAt = now,
        });

        Assert.AreEqual(ConflictResolutionOutcome.AutoResolvedServerWins, outcome);
    }

    [TestMethod]
    public async Task ResolveAsync_Strategy4_SmallDiff_DoesNotApply()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "local");

        var now = DateTime.UtcNow;
        // 3 minute diff — below the 5-minute threshold
        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            LocalModifiedAt = now,
            RemoteUpdatedAt = now.AddMinutes(-3),
        });

        // Falls through to conflict copy (Strategy 4 didn't fire, Strategy 5 requires text content)
        Assert.AreEqual(ConflictResolutionOutcome.ConflictCopyCreated, outcome);
    }

    // ── Strategy 5: Append-only detection ─────────────────────────────────

    [TestMethod]
    public async Task ResolveAsync_Strategy5_LocalExtendsServer_ReturnsLocalWins()
    {
        var localPath = Path.Combine(_tempDir, "log.txt");
        const string serverContent = "entry1\nentry2\n";
        const string localContent = "entry1\nentry2\nentry3\n"; // local extends server

        await File.WriteAllTextAsync(localPath, localContent);

        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
            LocalContent = localContent,
            ServerContent = serverContent,
        });

        Assert.AreEqual(ConflictResolutionOutcome.AutoResolvedLocalWins, outcome);
    }

    // ── AutoResolved event: DB record saved ────────────────────────────────

    [TestMethod]
    public async Task ResolveAsync_AutoResolved_SavesAutoResolvedConflictRecord()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "content");
        const string dbPath = "/test/state.db";

        await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
            LocalContentHash = "same",
            RemoteContentHash = "same",
            StateDatabasePath = dbPath,
        });

        _stateDb.Verify(db => db.SaveConflictRecordAsync(
            dbPath,
            It.Is<ConflictRecord>(r => r.AutoResolved && r.ResolvedAt != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── BuildConflictCopyPath ───────────────────────────────────────────────

    [TestMethod]
    public void BuildConflictCopyPath_ContainsConflictKeyword()
    {
        var path = Path.Combine(_tempDir, "document.docx");

        var result = ConflictResolver.BuildConflictCopyPath(path);

        StringAssert.Contains(result, "conflict");
        StringAssert.Contains(result, ".docx");
    }

    [TestMethod]
    public void BuildConflictCopyPath_PreservesDirectory()
    {
        var path = Path.Combine(_tempDir, "subdir", "file.txt");

        var result = ConflictResolver.BuildConflictCopyPath(path);

        var dir = Path.GetDirectoryName(result);
        Assert.AreEqual(Path.Combine(_tempDir, "subdir"), dir);
    }

    [TestMethod]
    public void BuildConflictCopyPath_WhenConflictExists_Increments()
    {
        // Create the first conflict file
        var path = Path.Combine(_tempDir, "report.pdf");
        var firstConflict = ConflictResolver.BuildConflictCopyPath(path);
        File.WriteAllText(firstConflict, "");

        var secondConflict = ConflictResolver.BuildConflictCopyPath(path);

        Assert.AreNotEqual(firstConflict, secondConflict);
        StringAssert.Contains(secondConflict, "1");
    }

    // ── Issue #55: Settings-driven conflict resolution ──────────────────────

    [TestMethod]
    public async Task ResolveAsync_AutoResolveDisabled_CreatesConflictCopyImmediately()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "content");

        _resolver.Settings = new ConflictResolutionSettings { AutoResolveEnabled = false };

        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
            LocalContentHash = "same",
            RemoteContentHash = "same", // Would be identical, but auto-resolve is off
        });

        Assert.AreEqual(ConflictResolutionOutcome.ConflictCopyCreated, outcome);
    }

    [TestMethod]
    public async Task ResolveAsync_IdenticalDisabled_SkipsStrategy1()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "content");

        _resolver.Settings = new ConflictResolutionSettings
        {
            EnabledStrategies = ["fast-forward", "clean-merge", "newer-wins", "append-only"],
        };

        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            RemoteUpdatedAt = DateTime.UtcNow,
            LocalContentHash = "same",
            RemoteContentHash = "same",
        });

        // "identical" disabled → falls through to conflict copy
        Assert.AreEqual(ConflictResolutionOutcome.ConflictCopyCreated, outcome);
    }

    [TestMethod]
    public async Task ResolveAsync_CustomThreshold_UsesSettings()
    {
        var localPath = Path.Combine(_tempDir, "file.txt");
        await File.WriteAllTextAsync(localPath, "content");

        // Set threshold to 1 minute (default is 5)
        _resolver.Settings = new ConflictResolutionSettings { NewerWinsThresholdMinutes = 1 };

        var now = DateTime.UtcNow;
        // 3-minute diff — would fail default 5-min threshold, but passes 1-min threshold
        var outcome = await _resolver.ResolveAsync(new ConflictInfo
        {
            LocalPath = localPath,
            NodeId = Guid.NewGuid(),
            LocalModifiedAt = now,
            RemoteUpdatedAt = now.AddMinutes(-3),
        });

        Assert.AreEqual(ConflictResolutionOutcome.AutoResolvedLocalWins, outcome);
    }

    [TestMethod]
    public void ConflictResolutionSettings_IsStrategyEnabled_CaseInsensitive()
    {
        var settings = new ConflictResolutionSettings();
        Assert.IsTrue(settings.IsStrategyEnabled("IDENTICAL"));
        Assert.IsTrue(settings.IsStrategyEnabled("Newer-Wins"));
        Assert.IsFalse(settings.IsStrategyEnabled("nonexistent"));
    }
}
