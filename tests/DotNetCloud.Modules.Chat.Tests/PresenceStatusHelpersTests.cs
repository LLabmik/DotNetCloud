using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Chat.UI;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for the shared 4-state presence display helpers used across chat UI presence
/// indicators (DM sidebar dots, DM thread header, member list rows).
/// </summary>
[TestClass]
public class PresenceStatusHelpersTests
{
    [TestMethod]
    [DataRow("Online", "presence-online")]
    [DataRow("Away", "presence-away")]
    [DataRow("DoNotDisturb", "presence-dnd")]
    [DataRow("Offline", "presence-offline")]
    [DataRow(null, "presence-offline")]
    [DataRow("", "presence-offline")]
    [DataRow("  ", "presence-offline")]
    [DataRow("unknown", "presence-offline")]
    public void GetCssClass_MapsEachState(string? status, string expected)
    {
        Assert.AreEqual(expected, PresenceStatusHelpers.GetCssClass(status));
    }

    [TestMethod]
    [DataRow("Online", "Online")]
    [DataRow("Away", "Idle")]
    [DataRow("DoNotDisturb", "Do Not Disturb")]
    [DataRow("Offline", "Offline")]
    [DataRow(null, "Offline")]
    [DataRow("", "Offline")]
    public void GetLabel_MapsEachStateToFriendlyText(string? status, string expected)
    {
        Assert.AreEqual(expected, PresenceStatusHelpers.GetLabel(status));
    }

    [TestMethod]
    [DataRow(PresenceState.Online, "Online")]
    [DataRow(PresenceState.Away, "Away")]
    [DataRow(PresenceState.DoNotDisturb, "DoNotDisturb")]
    [DataRow(PresenceState.Offline, "Offline")]
    public void ToStatusString_MatchesCanonicalNames(PresenceState state, string expected)
    {
        Assert.AreEqual(expected, PresenceStatusHelpers.ToStatusString(state));
    }

    [TestMethod]
    public void ToStatusString_DoesNotEmitJsonNumbers()
    {
        // The canonical wire representation is the enum NAME (DoNotDisturb), which both the
        // Blazor in-process UI and the SignalR/JSON wire format depend on.
        Assert.AreEqual("DoNotDisturb", PresenceStatusHelpers.ToStatusString(PresenceState.DoNotDisturb));
        Assert.AreNotEqual("3", PresenceStatusHelpers.ToStatusString(PresenceState.DoNotDisturb));
    }
}
