using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for <see cref="CallStateValidator"/> state machine enforcement.
/// </summary>
[TestClass]
public class CallStateValidatorTests
{
    // ── Valid transitions from Ringing ──────────────────────────────

    [TestMethod]
    public void IsValidTransition_RingingToConnecting_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Ringing, VideoCallState.Connecting));
    }

    [TestMethod]
    public void IsValidTransition_RingingToMissed_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Ringing, VideoCallState.Missed));
    }

    [TestMethod]
    public void IsValidTransition_RingingToRejected_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Ringing, VideoCallState.Rejected));
    }

    [TestMethod]
    public void IsValidTransition_RingingToFailed_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Ringing, VideoCallState.Failed));
    }

    // ── Invalid transitions from Ringing ───────────────────────────

    [TestMethod]
    public void IsValidTransition_RingingToActive_ReturnsFalse()
    {
        Assert.IsFalse(CallStateValidator.IsValidTransition(VideoCallState.Ringing, VideoCallState.Active));
    }

    [TestMethod]
    public void IsValidTransition_RingingToEnded_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Ringing, VideoCallState.Ended));
    }

    [TestMethod]
    public void IsValidTransition_RingingToOnHold_ReturnsFalse()
    {
        Assert.IsFalse(CallStateValidator.IsValidTransition(VideoCallState.Ringing, VideoCallState.OnHold));
    }

    [TestMethod]
    public void IsValidTransition_RingingToRinging_ReturnsFalse()
    {
        Assert.IsFalse(CallStateValidator.IsValidTransition(VideoCallState.Ringing, VideoCallState.Ringing));
    }

    // ── Valid transitions from Connecting ───────────────────────────

    [TestMethod]
    public void IsValidTransition_ConnectingToActive_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Connecting, VideoCallState.Active));
    }

    [TestMethod]
    public void IsValidTransition_ConnectingToFailed_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Connecting, VideoCallState.Failed));
    }

    [TestMethod]
    public void IsValidTransition_ConnectingToEnded_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Connecting, VideoCallState.Ended));
    }

    // ── Invalid transitions from Connecting ────────────────────────

    [TestMethod]
    public void IsValidTransition_ConnectingToRinging_ReturnsFalse()
    {
        Assert.IsFalse(CallStateValidator.IsValidTransition(VideoCallState.Connecting, VideoCallState.Ringing));
    }

    // ── Valid transitions from Active ──────────────────────────────

    [TestMethod]
    public void IsValidTransition_ActiveToEnded_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Active, VideoCallState.Ended));
    }

    [TestMethod]
    public void IsValidTransition_ActiveToOnHold_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Active, VideoCallState.OnHold));
    }

    [TestMethod]
    public void IsValidTransition_ActiveToFailed_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.Active, VideoCallState.Failed));
    }

    // ── Invalid transitions from Active ────────────────────────────

    [TestMethod]
    public void IsValidTransition_ActiveToRinging_ReturnsFalse()
    {
        Assert.IsFalse(CallStateValidator.IsValidTransition(VideoCallState.Active, VideoCallState.Ringing));
    }

    [TestMethod]
    public void IsValidTransition_ActiveToConnecting_ReturnsFalse()
    {
        Assert.IsFalse(CallStateValidator.IsValidTransition(VideoCallState.Active, VideoCallState.Connecting));
    }

    // ── Valid transitions from OnHold ──────────────────────────────

    [TestMethod]
    public void IsValidTransition_OnHoldToActive_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.OnHold, VideoCallState.Active));
    }

    [TestMethod]
    public void IsValidTransition_OnHoldToEnded_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.OnHold, VideoCallState.Ended));
    }

    [TestMethod]
    public void IsValidTransition_OnHoldToFailed_ReturnsTrue()
    {
        Assert.IsTrue(CallStateValidator.IsValidTransition(VideoCallState.OnHold, VideoCallState.Failed));
    }

    // ── Terminal states: no transitions allowed ────────────────────

    [TestMethod]
    [DataRow(VideoCallState.Ended)]
    [DataRow(VideoCallState.Missed)]
    [DataRow(VideoCallState.Rejected)]
    [DataRow(VideoCallState.Failed)]
    public void IsValidTransition_FromTerminalState_AllTransitionsReturnFalse(VideoCallState terminalState)
    {
        foreach (var target in Enum.GetValues<VideoCallState>())
        {
            Assert.IsFalse(
                CallStateValidator.IsValidTransition(terminalState, target),
                $"Expected {terminalState} → {target} to be invalid.");
        }
    }

    // ── ValidateTransition throws on invalid ───────────────────────

    [TestMethod]
    public void ValidateTransition_ValidTransition_DoesNotThrow()
    {
        CallStateValidator.ValidateTransition(VideoCallState.Ringing, VideoCallState.Connecting);
    }

    [TestMethod]
    public void ValidateTransition_InvalidTransition_ThrowsInvalidOperationException()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            CallStateValidator.ValidateTransition(VideoCallState.Ringing, VideoCallState.Active));

        Assert.IsTrue(ex.Message.Contains("Ringing"));
        Assert.IsTrue(ex.Message.Contains("Active"));
    }

    [TestMethod]
    public void ValidateTransition_FromTerminalState_ThrowsInvalidOperationException()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CallStateValidator.ValidateTransition(VideoCallState.Ended, VideoCallState.Active));
    }

    // ── IsTerminalState ────────────────────────────────────────────

    [TestMethod]
    [DataRow(VideoCallState.Ended, true)]
    [DataRow(VideoCallState.Missed, true)]
    [DataRow(VideoCallState.Rejected, true)]
    [DataRow(VideoCallState.Failed, true)]
    [DataRow(VideoCallState.Ringing, false)]
    [DataRow(VideoCallState.Connecting, false)]
    [DataRow(VideoCallState.Active, false)]
    [DataRow(VideoCallState.OnHold, false)]
    public void IsTerminalState_ReturnsExpected(VideoCallState state, bool expected)
    {
        Assert.AreEqual(expected, CallStateValidator.IsTerminalState(state));
    }

    // ── GetValidTargetStates ───────────────────────────────────────

    [TestMethod]
    public void GetValidTargetStates_FromRinging_ReturnsFiveStates()
    {
        var targets = CallStateValidator.GetValidTargetStates(VideoCallState.Ringing);
        Assert.AreEqual(5, targets.Count);
        Assert.IsTrue(targets.Contains(VideoCallState.Connecting));
        Assert.IsTrue(targets.Contains(VideoCallState.Ended));
        Assert.IsTrue(targets.Contains(VideoCallState.Missed));
        Assert.IsTrue(targets.Contains(VideoCallState.Rejected));
        Assert.IsTrue(targets.Contains(VideoCallState.Failed));
    }

    [TestMethod]
    public void GetValidTargetStates_FromConnecting_ReturnsThreeStates()
    {
        var targets = CallStateValidator.GetValidTargetStates(VideoCallState.Connecting);
        Assert.AreEqual(3, targets.Count);
        Assert.IsTrue(targets.Contains(VideoCallState.Active));
        Assert.IsTrue(targets.Contains(VideoCallState.Ended));
        Assert.IsTrue(targets.Contains(VideoCallState.Failed));
    }

    [TestMethod]
    public void GetValidTargetStates_FromActive_ReturnsThreeStates()
    {
        var targets = CallStateValidator.GetValidTargetStates(VideoCallState.Active);
        Assert.AreEqual(3, targets.Count);
        Assert.IsTrue(targets.Contains(VideoCallState.Ended));
        Assert.IsTrue(targets.Contains(VideoCallState.OnHold));
        Assert.IsTrue(targets.Contains(VideoCallState.Failed));
    }

    [TestMethod]
    public void GetValidTargetStates_FromTerminalState_ReturnsEmpty()
    {
        var targets = CallStateValidator.GetValidTargetStates(VideoCallState.Ended);
        Assert.AreEqual(0, targets.Count);
    }
}
