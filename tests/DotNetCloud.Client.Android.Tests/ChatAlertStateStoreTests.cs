using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests;

/// <summary>
/// Tests for <see cref="ChatAlertStateStore"/>: the entity-tag and high-water mark that survive between
/// background polls.
/// </summary>
[TestClass]
public class ChatAlertStateStoreTests
{
    private InMemoryPreferences _preferences = null!;
    private ChatAlertStateStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        _preferences = new InMemoryPreferences();
        _store = new ChatAlertStateStore(_preferences);
    }

    [TestMethod]
    public void GetETag_WhenNothingStored_ThenNull()
    {
        Assert.IsNull(_store.GetETag());
    }

    [TestMethod]
    public void SetETag_ThenRoundTrips()
    {
        _store.SetETag("\"abc123\"");

        Assert.AreEqual("\"abc123\"", _store.GetETag());
    }

    [TestMethod]
    public void SetETag_WhenNull_ThenClears()
    {
        _store.SetETag("\"abc123\"");

        _store.SetETag(null);

        Assert.IsNull(_store.GetETag());
    }

    [TestMethod]
    public void GetLastAcknowledgedChangedAtUtc_WhenNothingStored_ThenNull()
    {
        Assert.IsNull(_store.GetLastAcknowledgedChangedAtUtc());
    }

    [TestMethod]
    public void SetLastAcknowledgedChangedAtUtc_ThenRoundTripsAsUtc()
    {
        var value = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        _store.SetLastAcknowledgedChangedAtUtc(value);
        var read = _store.GetLastAcknowledgedChangedAtUtc();

        Assert.AreEqual(value, read);
        Assert.AreEqual(DateTimeKind.Utc, read!.Value.Kind);
    }

    [TestMethod]
    public void SetLastAcknowledgedChangedAtUtc_WhenLocalValue_ThenStoredAsUtc()
    {
        var local = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Local);

        _store.SetLastAcknowledgedChangedAtUtc(local);

        Assert.AreEqual(local.ToUniversalTime(), _store.GetLastAcknowledgedChangedAtUtc());
    }

    [TestMethod]
    public void SetLastAcknowledgedChangedAtUtc_WhenNull_ThenClears()
    {
        _store.SetLastAcknowledgedChangedAtUtc(DateTime.UtcNow);

        _store.SetLastAcknowledgedChangedAtUtc(null);

        Assert.IsNull(_store.GetLastAcknowledgedChangedAtUtc());
    }

    [TestMethod]
    public void GetHasUnread_WhenNothingStored_ThenFalse()
    {
        Assert.IsFalse(_store.GetHasUnread());
    }

    [TestMethod]
    public void SetHasUnread_ThenRoundTrips()
    {
        _store.SetHasUnread(true);
        Assert.IsTrue(_store.GetHasUnread());

        _store.SetHasUnread(false);
        Assert.IsFalse(_store.GetHasUnread());
    }

    [TestMethod]
    public void ParseUtc_WhenUnparsable_ThenNull()
    {
        Assert.IsNull(ChatAlertStateStore.ParseUtc("not-a-timestamp"));
    }

    [TestMethod]
    public void ParseUtc_WhenBlank_ThenNull()
    {
        Assert.IsNull(ChatAlertStateStore.ParseUtc("   "));
        Assert.IsNull(ChatAlertStateStore.ParseUtc(null));
    }

    [TestMethod]
    public void ParseUtc_WhenRoundTripValue_ThenReturnsUtc()
    {
        var parsed = ChatAlertStateStore.ParseUtc("2026-01-02T03:04:05.0000000Z");

        Assert.IsNotNull(parsed);
        Assert.AreEqual(DateTimeKind.Utc, parsed!.Value.Kind);
        Assert.AreEqual(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), parsed.Value);
    }
}
