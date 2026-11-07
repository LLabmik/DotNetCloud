using DotNetCloud.Core.Server.Supervisor;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// Tests for <see cref="ModuleCredentialStore"/>: issuance, rotation, revocation and the non-secret
/// lifecycle record (decision D1 — credentials stay ephemeral, never persisted).
/// </summary>
[TestClass]
public class ModuleCredentialStoreTests
{
    private const string ModuleId = "dotnetcloud.notes";

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));

    private ModuleCredentialStore CreateStore() => new(_clock);

    [TestMethod]
    public void Issue_ReturnsBase64TokenAndRecordsNonSecretStatus()
    {
        var store = CreateStore();

        var token = store.Issue(ModuleId);
        var status = store.GetStatus(ModuleId);

        Assert.AreEqual(44, token.Length, "32 CSPRNG bytes encode to 44 base64 characters.");
        Assert.IsNotNull(status);
        Assert.IsTrue(status.IsActive);
        Assert.AreEqual(_clock.GetUtcNow(), status.IssuedAtUtc);
        Assert.IsNull(status.RotatedAtUtc);
        Assert.IsNull(status.RevokedAtUtc);
        Assert.AreEqual(ModuleCredentialStore.HintLength, status.Hint!.Length);
        StringAssert.StartsWith(token, status.Hint, "The hint is a prefix of the token.");
        Assert.AreNotEqual(token, status.Hint, "The status must never expose the token.");
    }

    [TestMethod]
    public void Issue_Twice_ReplacesTokenAndKeepsOriginalIssueTime()
    {
        var store = CreateStore();
        var first = store.Issue(ModuleId);

        _clock.Advance(TimeSpan.FromMinutes(5));
        var second = store.Issue(ModuleId);

        Assert.AreNotEqual(first, second);
        Assert.IsTrue(store.Validate(ModuleId, second));
        Assert.IsFalse(store.Validate(ModuleId, first), "Re-issuing must invalidate the previous token.");

        var status = store.GetStatus(ModuleId);
        Assert.IsNotNull(status);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero), status.IssuedAtUtc);
        Assert.IsNull(status.RotatedAtUtc, "Issue is not a rotation; Rotate records the rotated-at stamp.");
    }

    [TestMethod]
    public void Rotate_InvalidatesPreviousTokenAndStampsRotation()
    {
        var store = CreateStore();
        var first = store.Issue(ModuleId);

        _clock.Advance(TimeSpan.FromMinutes(1));
        var rotated = store.Rotate(ModuleId);

        Assert.AreNotEqual(first, rotated);
        Assert.IsFalse(store.Validate(ModuleId, first));
        Assert.IsTrue(store.Validate(ModuleId, rotated));

        var status = store.GetStatus(ModuleId);
        Assert.IsNotNull(status);
        Assert.AreEqual(_clock.GetUtcNow(), status.RotatedAtUtc);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero), status.IssuedAtUtc);
    }

    [TestMethod]
    public void Revoke_ClearsTokenButKeepsObservableRecord()
    {
        var store = CreateStore();
        var token = store.Issue(ModuleId);

        _clock.Advance(TimeSpan.FromMinutes(2));
        store.Revoke(ModuleId);

        Assert.IsFalse(store.Validate(ModuleId, token));
        Assert.IsFalse(store.TryGetToken(ModuleId, out _));
        Assert.IsFalse(store.HasCredential(ModuleId));

        var status = store.GetStatus(ModuleId);
        Assert.IsNotNull(status);
        Assert.IsFalse(status.IsActive);
        Assert.AreEqual(_clock.GetUtcNow(), status.RevokedAtUtc);
        Assert.IsNotNull(status.Hint, "The hint is retained for audit correlation, never the token.");
    }

    [TestMethod]
    public void Issue_AfterRevoke_ReactivatesCredential()
    {
        var store = CreateStore();
        store.Issue(ModuleId);
        store.Revoke(ModuleId);

        var token = store.Issue(ModuleId);
        var status = store.GetStatus(ModuleId);

        Assert.IsTrue(store.Validate(ModuleId, token));
        Assert.IsNotNull(status);
        Assert.IsTrue(status.IsActive);
        Assert.IsNull(status.RevokedAtUtc);
    }

    [TestMethod]
    public void GetStatus_UnknownModule_ReturnsNull()
    {
        Assert.IsNull(CreateStore().GetStatus("dotnetcloud.unknown"));
    }

    [TestMethod]
    public void Validate_UnknownModuleOrEmptyToken_ReturnsFalse()
    {
        var store = CreateStore();
        store.Issue(ModuleId);

        Assert.IsFalse(store.Validate("dotnetcloud.other", "token"));
        Assert.IsFalse(store.Validate(ModuleId, string.Empty));
        Assert.IsFalse(store.Validate(ModuleId, null));
    }

    [TestMethod]
    public void TryGetToken_UnknownModule_ReturnsFalse()
    {
        Assert.IsFalse(CreateStore().TryGetToken("dotnetcloud.unknown", out var token));
        Assert.AreEqual(string.Empty, token);
    }

    /// <summary>Controllable clock for lifecycle timestamps.</summary>
    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }
}
