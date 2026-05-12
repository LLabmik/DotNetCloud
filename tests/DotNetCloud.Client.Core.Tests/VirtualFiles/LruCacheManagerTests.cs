using DotNetCloud.Client.Core.VirtualFiles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.VirtualFiles;

[TestClass]
public sealed class LruCacheManagerTests
{
    private VirtualFileSettings _settings = null!;
    private LruCacheManager _cache = null!;

    [TestInitialize]
    public void Initialize()
    {
        _settings = new VirtualFileSettings();
        _cache = new LruCacheManager(_settings, NullLogger<LruCacheManager>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _cache.Dispose();
    }

    // ── Basic put/get ──────────────────────────────────────────────────

    [TestMethod]
    public void PutAndGet_StoresAndRetrievesData()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        _cache["/path/to/file.bin"] = data;

        var result = _cache["/path/to/file.bin"];

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(data, result);
    }

    [TestMethod]
    public void Get_MissingEntry_ReturnsNull()
    {
        var result = _cache["/nonexistent/path"];

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Put_NullData_RemovesEntry()
    {
        _cache["/path/to/file.bin"] = [1, 2, 3];
        _cache["/path/to/file.bin"] = null;

        var result = _cache["/path/to/file.bin"];
        Assert.IsNull(result);
    }

    // ── Cache size tracking ────────────────────────────────────────────

    [TestMethod]
    public void CurrentSizeBytes_ReflectsTotalCachedContent()
    {
        _cache["file1.bin"] = new byte[100];
        _cache["file2.bin"] = new byte[200];

        Assert.AreEqual(300, _cache.CurrentSizeBytes);
    }

    [TestMethod]
    public void Remove_DecrementsSize()
    {
        _cache["file.bin"] = new byte[500];
        Assert.AreEqual(500, _cache.CurrentSizeBytes);

        _cache.Remove("file.bin");

        Assert.AreEqual(0, _cache.CurrentSizeBytes);
        Assert.IsNull(_cache["file.bin"]);
    }

    [TestMethod]
    public void Clear_ResetsSizeToZero()
    {
        _cache["a.bin"] = new byte[100];
        _cache["b.bin"] = new byte[200];
        _cache.Clear();

        Assert.AreEqual(0, _cache.CurrentSizeBytes);
        Assert.AreEqual(0, _cache.EntryCount);
    }

    // ── LRU eviction ───────────────────────────────────────────────────

    [TestMethod]
    public void EvictIfNeeded_EvictsOldestFirst_WhenOverLimit()
    {
        _settings.MaxCacheSizeBytes = 150;

        _cache["oldest.bin"] = new byte[100];
        // Ensure oldest entry has older timestamp
        Thread.Sleep(50);
        _cache["newest.bin"] = new byte[100];

        // Total: 200 bytes, max: 150 — eviction already triggered inside the
        // indexer setter. Check post-condition state instead of return value.
        Assert.IsNull(_cache["oldest.bin"]);  // LRU evicted
        Assert.IsNotNull(_cache["newest.bin"]); // Kept
        Assert.IsTrue(_cache.CurrentSizeBytes <= _settings.MaxCacheSizeBytes);
    }

    [TestMethod]
    public void EvictIfNeeded_NoEviction_WhenUnderLimit()
    {
        _settings.MaxCacheSizeBytes = 1000;

        _cache["a.bin"] = new byte[100];
        _cache["b.bin"] = new byte[200];

        var evicted = _cache.EvictIfNeeded();

        Assert.AreEqual(0, evicted);
        Assert.IsNotNull(_cache["a.bin"]);
        Assert.IsNotNull(_cache["b.bin"]);
    }

    [TestMethod]
    public void EvictIfNeeded_NoEviction_WhenLimitIsZero()
    {
        _settings.MaxCacheSizeBytes = 0;

        _cache["a.bin"] = new byte[5000];

        var evicted = _cache.EvictIfNeeded();

        Assert.AreEqual(0, evicted);
    }

    // ── Pin exemption ──────────────────────────────────────────────────

    [TestMethod]
    public void PinnedEntry_SurvivesEviction()
    {
        _settings.MaxCacheSizeBytes = 100;
        _settings.PinList.Add("/path/to/pinned.bin");

        _cache["/path/to/pinned.bin"] = new byte[80];
        Thread.Sleep(50);
        _cache["other.bin"] = new byte[80];

        // Total: 160 bytes, max: 100 — eviction already triggered inside the
        // indexer setter. Check post-condition state instead of return value.
        Assert.IsNotNull(_cache["/path/to/pinned.bin"]); // Pinned survives
        Assert.IsNull(_cache["other.bin"]); // Unpinned evicted
        Assert.IsTrue(_cache.CurrentSizeBytes <= _settings.MaxCacheSizeBytes);
    }

    [TestMethod]
    public void PinnedEntry_OnlyPinnedRemaining_StopsEviction()
    {
        _settings.MaxCacheSizeBytes = 100;
        _settings.PinList.Add("/path/to/pinned.bin");

        _cache["/path/to/pinned.bin"] = new byte[200]; // Exceeds limit alone

        var evicted = _cache.EvictIfNeeded();

        Assert.AreEqual(0, evicted); // Can't evict pinned
        Assert.IsNotNull(_cache["/path/to/pinned.bin"]);
        Assert.IsTrue(_cache.CurrentSizeBytes > _settings.MaxCacheSizeBytes); // Over limit but pinned
    }

    // ── TryPeek ────────────────────────────────────────────────────────

    [TestMethod]
    public void TryPeek_ExistingEntry_ReturnsTrueAndData()
    {
        var data = new byte[] { 10, 20, 30 };
        _cache["file.bin"] = data;

        var found = _cache.TryPeek("file.bin", out var result);

        Assert.IsTrue(found);
        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(data, result);
    }

    [TestMethod]
    public void TryPeek_MissingEntry_ReturnsFalse()
    {
        var found = _cache.TryPeek("missing.bin", out var result);

        Assert.IsFalse(found);
        Assert.IsNull(result);
    }

    // ── Manual eviction count ──────────────────────────────────────────

    [TestMethod]
    public void EvictIfNeeded_ManualCall_ReturnsEvictedCount()
    {
        // Add entries with a high limit so auto-eviction doesn't trigger,
        // then lower the limit and call EvictIfNeeded manually.
        _settings.MaxCacheSizeBytes = 1000;
        _cache["a.bin"] = new byte[100];
        _cache["b.bin"] = new byte[100];
        Assert.AreEqual(200, _cache.CurrentSizeBytes);

        // Lower limit and evict manually
        _settings.MaxCacheSizeBytes = 150;
        var evicted = _cache.EvictIfNeeded();

        Assert.AreEqual(1, evicted);
        Assert.IsTrue(_cache.CurrentSizeBytes <= 150);
    }

    // ── Entry count ────────────────────────────────────────────────────

    [TestMethod]
    public void EntryCount_ReflectsNumberOfCachedFiles()
    {
        Assert.AreEqual(0, _cache.EntryCount);

        _cache["a.bin"] = [1];
        _cache["b.bin"] = [2];
        _cache["c.bin"] = [3];

        Assert.AreEqual(3, _cache.EntryCount);
    }
}
