using DotNetCloud.Core.Server.Consent;

namespace DotNetCloud.Core.Server.Tests.Consent;

/// <summary>
/// Tests for the in-flight consent prompt store: ids must be unguessable, bound to one user,
/// single-use, and time-limited.
/// </summary>
[TestClass]
public class ConsentRequestStoreTests
{
    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly Guid UserId = Guid.CreateVersion7();

    private static (ConsentRequestStore Store, ManualTimeProvider Time) CreateStore()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        return (new ConsentRequestStore(time), time);
    }

    private static string CreatePending(ConsentRequestStore store, Guid? userId = null) =>
        store.Create(
            userId ?? UserId,
            "dnc-module-vendor-receipts",
            "vendor.receipts",
            "Receipt Scanner",
            ["files:read"],
            "?client_id=dnc-module-vendor-receipts&scope=files:read");

    [TestMethod]
    public void Create_ProducesAnUnguessableUrlSafeId()
    {
        var (store, _) = CreateStore();

        var first = CreatePending(store);
        var second = CreatePending(store);

        Assert.AreNotEqual(first, second);
        Assert.IsTrue(first.Length >= 40, $"Id '{first}' is too short to be unguessable.");
        Assert.IsFalse(first.Contains('+') || first.Contains('/') || first.Contains('='));
    }

    [TestMethod]
    public void Find_KnownRequest_ReturnsIt()
    {
        var (store, _) = CreateStore();
        var id = CreatePending(store);

        var request = store.Find(id, UserId);

        Assert.IsNotNull(request);
        Assert.AreEqual("vendor.receipts", request.ModuleId);
        CollectionAssert.AreEqual(new[] { "files:read" }, request.Scopes.ToArray());
        Assert.AreEqual(ConsentDecision.Pending, request.Decision);
    }

    [TestMethod]
    public void Find_DifferentUser_IsRejected()
    {
        var (store, _) = CreateStore();
        var id = CreatePending(store);

        Assert.IsNull(store.Find(id, Guid.CreateVersion7()));
    }

    [TestMethod]
    public void Find_AfterLifetime_IsRejectedAndEvicted()
    {
        var (store, time) = CreateStore();
        var id = CreatePending(store);

        time.Now = time.Now.Add(ConsentRequestStore.Lifetime).Add(TimeSpan.FromSeconds(1));

        Assert.IsNull(store.Find(id, UserId));
        Assert.IsNull(store.Find(id, UserId));
    }

    [TestMethod]
    public void RecordDecision_Approved_MarksTheRequest()
    {
        var (store, _) = CreateStore();
        var id = CreatePending(store);

        Assert.IsTrue(store.RecordDecision(id, UserId, ConsentDecision.Approved));

        var request = store.Find(id, UserId);
        Assert.IsNotNull(request);
        Assert.AreEqual(ConsentDecision.Approved, request.Decision);
    }

    [TestMethod]
    public void RecordDecision_Twice_IsRejected()
    {
        var (store, _) = CreateStore();
        var id = CreatePending(store);

        Assert.IsTrue(store.RecordDecision(id, UserId, ConsentDecision.Denied));
        Assert.IsFalse(store.RecordDecision(id, UserId, ConsentDecision.Approved));
    }

    [TestMethod]
    public void RecordDecision_DifferentUser_IsRejected()
    {
        var (store, _) = CreateStore();
        var id = CreatePending(store);

        Assert.IsFalse(store.RecordDecision(id, Guid.CreateVersion7(), ConsentDecision.Approved));
        Assert.AreEqual(ConsentDecision.Pending, store.Find(id, UserId)!.Decision);
    }

    [TestMethod]
    public void Consume_RemovesTheRequest()
    {
        var (store, _) = CreateStore();
        var id = CreatePending(store);
        store.RecordDecision(id, UserId, ConsentDecision.Approved);

        var consumed = store.Consume(id, UserId);

        Assert.IsNotNull(consumed);
        Assert.AreEqual(ConsentDecision.Approved, consumed.Decision);
        Assert.IsNull(store.Find(id, UserId), "A captured prompt id must not be replayable.");
        Assert.IsNull(store.Consume(id, UserId));
    }

    [TestMethod]
    public void Create_BeyondCapacity_DropsTheOldestEntries()
    {
        var (store, _) = CreateStore();
        string lastId = string.Empty;

        for (var index = 0; index <= ConsentRequestStore.MaxEntries; index++)
        {
            lastId = CreatePending(store);
        }

        Assert.IsNotNull(store.Find(lastId, UserId), "The newest prompt must survive pruning.");
    }
}
