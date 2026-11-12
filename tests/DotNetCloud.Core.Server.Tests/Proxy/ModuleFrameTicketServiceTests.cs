using DotNetCloud.Core.Server.Proxy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

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

    [TestMethod]
    public void Issue_ConfiguredLifetime_IsHonoured()
    {
        var clock = new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Modules:Ui:FrameTicketMinutes"] = "1" })
            .Build();

        var service = new ModuleFrameTicketService(
            new EphemeralDataProtectionProvider(), clock, configuration);

        var ticket = service.Issue(Guid.CreateVersion7(), ModuleId);
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.IsFalse(service.TryValidate(ticket, ModuleId, out _));
    }

    [TestMethod]
    public void DefaultLifetime_CoversAWorkingSession()
    {
        // Fifteen minutes left an open viewer with 401s and no way to re-mint except a reload, which
        // threw away whatever the frame was in the middle of.
        Assert.IsTrue(
            ModuleFrameTicketService.Lifetime >= TimeSpan.FromHours(1),
            "the default frame-ticket lifetime must outlast a normal working session");
    }

    [TestMethod]
    public void Issue_TwiceForTheSameModule_ReusesTheTicket()
    {
        // The ticket is part of the iframe src. A new string per call made the src change on every
        // render, which restarted the frame navigation and aborted the in-flight load.
        var clock = new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56));
        var service = new ModuleFrameTicketService(new EphemeralDataProtectionProvider(), clock);
        var userId = Guid.CreateVersion7();

        var first = service.Issue(userId, ModuleId);
        var second = service.Issue(userId, ModuleId);

        Assert.AreEqual(first, second, "the same user and module must get a stable ticket while it is valid");
    }

    [TestMethod]
    public void Issue_DifferentModules_GetDifferentTickets()
    {
        var clock = new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56));
        var service = new ModuleFrameTicketService(new EphemeralDataProtectionProvider(), clock);
        var userId = Guid.CreateVersion7();

        Assert.AreNotEqual(
            service.Issue(userId, ModuleId),
            service.Issue(userId, "dotnetcloud.photos"));
    }

    [TestMethod]
    public void Issue_NearExpiry_MintsAFreshTicket()
    {
        var clock = new FixedClock(DateTimeOffset.UnixEpoch.AddYears(56));
        var service = new ModuleFrameTicketService(new EphemeralDataProtectionProvider(), clock);
        var userId = Guid.CreateVersion7();

        var first = service.Issue(userId, ModuleId);
        clock.Advance(ModuleFrameTicketService.Lifetime - TimeSpan.FromMinutes(1));
        var second = service.Issue(userId, ModuleId);

        Assert.AreNotEqual(first, second, "a ticket that is about to expire must not be handed out again");
    }
}
