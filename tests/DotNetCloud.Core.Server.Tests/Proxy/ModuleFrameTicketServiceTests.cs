using DotNetCloud.Core.Server.Proxy;
using Microsoft.AspNetCore.DataProtection;

namespace DotNetCloud.Core.Server.Tests.Proxy;

/// <summary>
/// Guards the frame ticket: the credential a sandboxed (opaque-origin) module UI uses because the
/// browser will never attach its SameSite session cookie to the frame's requests.
/// </summary>
[TestClass]
public class ModuleFrameTicketServiceTests
{
    private const string ModuleId = "dotnetcloud.bookmarks";

    private sealed class FixedClock : TimeProvider
    {
        private DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private static ModuleFrameTicketService CreateService(TimeProvider clock) =>
        new(new EphemeralDataProtectionProvider(), clock);

    [TestMethod]
    public void Issue_ThenValidate_ReturnsTheSameUser()
    {
        var clock = new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56));
        var service = CreateService(clock);
        var userId = Guid.CreateVersion7();

        var ticket = service.Issue(userId, ModuleId);

        Assert.IsTrue(service.TryValidate(ticket, ModuleId, out var validated));
        Assert.AreEqual(userId, validated);
    }

    [TestMethod]
    public void TryValidate_TicketIssuedForAnotherModule_IsRejected()
    {
        var service = CreateService(new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56)));
        var ticket = service.Issue(Guid.CreateVersion7(), ModuleId);

        Assert.IsFalse(service.TryValidate(ticket, "dotnetcloud.email", out var userId));
        Assert.AreEqual(Guid.Empty, userId);
    }

    [TestMethod]
    public void TryValidate_ExpiredTicket_IsRejected()
    {
        var clock = new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56));
        var service = CreateService(clock);
        var ticket = service.Issue(Guid.CreateVersion7(), ModuleId);

        clock.Advance(ModuleFrameTicketService.Lifetime + TimeSpan.FromSeconds(1));

        Assert.IsFalse(service.TryValidate(ticket, ModuleId, out _));
    }

    [TestMethod]
    public void TryValidate_TamperedTicket_IsRejected()
    {
        var service = CreateService(new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56)));
        var ticket = service.Issue(Guid.CreateVersion7(), ModuleId);
        var tampered = ticket[..^2] + (ticket[^1] == 'A' ? "BB" : "AA");

        Assert.IsFalse(service.TryValidate(tampered, ModuleId, out _));
    }

    [TestMethod]
    public void TryValidate_TicketFromADifferentKeyRing_IsRejected()
    {
        // A ticket minted by another core (or before a key rotation) must not be accepted.
        var foreign = CreateService(new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56)));
        var ours = CreateService(new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56)));
        var ticket = foreign.Issue(Guid.CreateVersion7(), ModuleId);

        Assert.IsFalse(ours.TryValidate(ticket, ModuleId, out _));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not-a-ticket")]
    public void TryValidate_EmptyOrGarbage_IsRejected(string ticket)
    {
        var service = CreateService(new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56)));

        Assert.IsFalse(service.TryValidate(ticket, ModuleId, out _));
    }

    [TestMethod]
    public void Issue_EmptyUser_Throws()
    {
        var service = CreateService(new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56)));

        Assert.ThrowsExactly<ArgumentException>(() => service.Issue(Guid.Empty, ModuleId));
    }
}
