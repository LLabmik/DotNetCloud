using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests;

/// <summary>
/// Tests for <see cref="ChatAlertCadence"/>, the poll cadence policy behind
/// <c>docs/ANDROID_UNIFIEDPUSH_PLAN.md</c> §12.9.
/// </summary>
[TestClass]
public class ChatAlertCadenceTests
{
    [TestMethod]
    public void ResolveNextDelay_WhenNoSession_ThenNull()
    {
        var delay = ChatAlertCadence.ResolveNextDelay(
            new ChatAlertPollState(HasSession: false, HasUnread: false, IsDozing: false));

        Assert.IsNull(delay);
    }

    [TestMethod]
    public void ResolveNextDelay_WhenNothingUnread_ThenIdleInterval()
    {
        var delay = ChatAlertCadence.ResolveNextDelay(
            new ChatAlertPollState(HasSession: true, HasUnread: false, IsDozing: false));

        Assert.AreEqual(ChatAlertCadence.Idle, delay);
    }

    [TestMethod]
    public void ResolveNextDelay_WhenMessagesAreWaiting_ThenActiveInterval()
    {
        var delay = ChatAlertCadence.ResolveNextDelay(
            new ChatAlertPollState(HasSession: true, HasUnread: true, IsDozing: false));

        Assert.AreEqual(ChatAlertCadence.Active, delay);
    }

    [TestMethod]
    public void ResolveNextDelay_WhenDozing_ThenDozingInterval()
    {
        var delay = ChatAlertCadence.ResolveNextDelay(
            new ChatAlertPollState(HasSession: true, HasUnread: true, IsDozing: true));

        Assert.AreEqual(ChatAlertCadence.Dozing, delay);
    }

    [TestMethod]
    public void ResolveNextDelay_WhenDozingAndNoSession_ThenNull()
    {
        var delay = ChatAlertCadence.ResolveNextDelay(
            new ChatAlertPollState(HasSession: false, HasUnread: false, IsDozing: true));

        Assert.IsNull(delay);
    }

    [TestMethod]
    public void ResolveNextDelay_WhenDozing_ThenIgnoresUnreadState()
    {
        var withUnread = ChatAlertCadence.ResolveNextDelay(
            new ChatAlertPollState(HasSession: true, HasUnread: true, IsDozing: true));
        var withoutUnread = ChatAlertCadence.ResolveNextDelay(
            new ChatAlertPollState(HasSession: true, HasUnread: false, IsDozing: true));

        Assert.AreEqual(withoutUnread, withUnread);
    }

    [TestMethod]
    public void ResolveNextDelay_ThenActiveIsShorterThanIdleWhichIsShorterThanDozing()
    {
        Assert.IsTrue(ChatAlertCadence.Active < ChatAlertCadence.Idle);
        Assert.IsTrue(ChatAlertCadence.Idle < ChatAlertCadence.Dozing);
    }

    [TestMethod]
    public void RequiresExactAlarm_WhenDozingAndGranted_ThenTrue()
    {
        var state = new ChatAlertPollState(HasSession: true, HasUnread: true, IsDozing: true);

        Assert.IsTrue(ChatAlertCadence.RequiresExactAlarm(state, exactAlarmPermissionGranted: true));
    }

    [TestMethod]
    public void RequiresExactAlarm_WhenDozingButNotGranted_ThenFalse()
    {
        var state = new ChatAlertPollState(HasSession: true, HasUnread: true, IsDozing: true);

        Assert.IsFalse(ChatAlertCadence.RequiresExactAlarm(state, exactAlarmPermissionGranted: false));
    }

    [TestMethod]
    public void RequiresExactAlarm_WhenNotDozing_ThenFalse()
    {
        var state = new ChatAlertPollState(HasSession: true, HasUnread: true, IsDozing: false);

        Assert.IsFalse(ChatAlertCadence.RequiresExactAlarm(state, exactAlarmPermissionGranted: true));
    }

    [TestMethod]
    public void RequiresExactAlarm_WhenNoSession_ThenFalse()
    {
        var state = new ChatAlertPollState(HasSession: false, HasUnread: true, IsDozing: true);

        Assert.IsFalse(ChatAlertCadence.RequiresExactAlarm(state, exactAlarmPermissionGranted: true));
    }
}
