using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.UI;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="VideoCallDialog"/> layout, state formatting, and initials logic.
/// </summary>
[TestClass]
public class VideoCallDialogTests
{
    // ── GetInitials Tests ───────────────────────────────────────────

    [TestMethod]
    public void GetInitials_WithTwoWordName_ReturnsFirstAndLastInitials()
    {
        Assert.AreEqual("JD", VideoCallDialog.GetInitials("John Doe"));
    }

    [TestMethod]
    public void GetInitials_WithThreeWordName_ReturnsFirstAndLastInitials()
    {
        Assert.AreEqual("JS", VideoCallDialog.GetInitials("John Michael Smith"));
    }

    [TestMethod]
    public void GetInitials_WithSingleWordTwoChars_ReturnsTwoUpperChars()
    {
        Assert.AreEqual("AL", VideoCallDialog.GetInitials("Alice"));
    }

    [TestMethod]
    public void GetInitials_WithSingleChar_ReturnsSingleUpperChar()
    {
        Assert.AreEqual("A", VideoCallDialog.GetInitials("A"));
    }

    [TestMethod]
    public void GetInitials_WithNull_ReturnsQuestionMark()
    {
        Assert.AreEqual("?", VideoCallDialog.GetInitials(null));
    }

    [TestMethod]
    public void GetInitials_WithEmptyString_ReturnsQuestionMark()
    {
        Assert.AreEqual("?", VideoCallDialog.GetInitials(""));
    }

    [TestMethod]
    public void GetInitials_WithWhitespaceOnly_ReturnsQuestionMark()
    {
        Assert.AreEqual("?", VideoCallDialog.GetInitials("   "));
    }

    [TestMethod]
    public void GetInitials_WithLowercaseName_ReturnsUppercase()
    {
        Assert.AreEqual("JD", VideoCallDialog.GetInitials("john doe"));
    }

    [TestMethod]
    public void GetInitials_WithExtraSpaces_TrimsAndWorks()
    {
        Assert.AreEqual("JD", VideoCallDialog.GetInitials("  John   Doe  "));
    }

    // ── FormatCallState Tests ───────────────────────────────────────

    [TestMethod]
    public void FormatCallState_Ringing_ReturnsRingingEllipsis()
    {
        Assert.AreEqual("Ringing...", VideoCallDialog.FormatCallState("Ringing"));
    }

    [TestMethod]
    public void FormatCallState_Connecting_ReturnsConnectingEllipsis()
    {
        Assert.AreEqual("Connecting...", VideoCallDialog.FormatCallState("Connecting"));
    }

    [TestMethod]
    public void FormatCallState_Active_ReturnsInCall()
    {
        Assert.AreEqual("In Call", VideoCallDialog.FormatCallState("Active"));
    }

    [TestMethod]
    public void FormatCallState_OnHold_ReturnsOnHold()
    {
        Assert.AreEqual("On Hold", VideoCallDialog.FormatCallState("OnHold"));
    }

    [TestMethod]
    public void FormatCallState_Ended_ReturnsCallEnded()
    {
        Assert.AreEqual("Call Ended", VideoCallDialog.FormatCallState("Ended"));
    }

    [TestMethod]
    public void FormatCallState_Missed_ReturnsMissed()
    {
        Assert.AreEqual("Missed", VideoCallDialog.FormatCallState("Missed"));
    }

    [TestMethod]
    public void FormatCallState_Rejected_ReturnsRejected()
    {
        Assert.AreEqual("Rejected", VideoCallDialog.FormatCallState("Rejected"));
    }

    [TestMethod]
    public void FormatCallState_Failed_ReturnsFailed()
    {
        Assert.AreEqual("Failed", VideoCallDialog.FormatCallState("Failed"));
    }

    [TestMethod]
    public void FormatCallState_Unknown_ReturnsAsIs()
    {
        Assert.AreEqual("SomeOther", VideoCallDialog.FormatCallState("SomeOther"));
    }

    // ── Layout Tests ────────────────────────────────────────────────

    [TestMethod]
    public void WhenNoRemoteParticipants_LayoutClassIsSolo()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetRemoteParticipants([]);

        Assert.AreEqual("layout-solo", dialog.TestLayoutClass);
    }

    [TestMethod]
    public void WhenOneRemoteParticipant_LayoutClassIsPair()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetRemoteParticipants([CreateParticipant()]);

        Assert.AreEqual("layout-pair", dialog.TestLayoutClass);
    }

    [TestMethod]
    public void WhenTwoRemoteParticipants_LayoutClassIsTrio()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetRemoteParticipants([CreateParticipant(), CreateParticipant()]);

        Assert.AreEqual("layout-trio", dialog.TestLayoutClass);
    }

    [TestMethod]
    public void WhenThreeOrMoreRemoteParticipants_LayoutClassIsGrid()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetRemoteParticipants([CreateParticipant(), CreateParticipant(), CreateParticipant()]);

        Assert.AreEqual("layout-grid", dialog.TestLayoutClass);
    }

    [TestMethod]
    public void WhenNoRemoteParticipants_GridLayoutIs1x1()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetRemoteParticipants([]);

        Assert.AreEqual("1x1", dialog.TestGridLayoutName);
    }

    [TestMethod]
    public void WhenOneRemoteParticipant_GridLayoutIs2x1()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetRemoteParticipants([CreateParticipant()]);

        Assert.AreEqual("2x1", dialog.TestGridLayoutName);
    }

    [TestMethod]
    public void WhenTwoRemoteParticipants_GridLayoutIs2x1Pip()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetRemoteParticipants([CreateParticipant(), CreateParticipant()]);

        Assert.AreEqual("2x1-pip", dialog.TestGridLayoutName);
    }

    [TestMethod]
    public void WhenThreeOrMoreRemoteParticipants_GridLayoutIs2x2()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetRemoteParticipants([CreateParticipant(), CreateParticipant(), CreateParticipant()]);

        Assert.AreEqual("2x2", dialog.TestGridLayoutName);
    }

    // ── Participant Count Tests ─────────────────────────────────────

    [TestMethod]
    public void TotalParticipantCount_IncludesLocalUser()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetRemoteParticipants([CreateParticipant(), CreateParticipant()]);

        Assert.AreEqual(3, dialog.TestTotalParticipantCount);
    }

    [TestMethod]
    public void TotalParticipantCount_WithNoRemote_IsOne()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetRemoteParticipants([]);

        Assert.AreEqual(1, dialog.TestTotalParticipantCount);
    }

    // ── Controls Disabled State Tests ───────────────────────────────

    [TestMethod]
    [DataRow("Ringing", true)]
    [DataRow("Connecting", false)]
    [DataRow("Active", false)]
    [DataRow("OnHold", false)]
    [DataRow("Ended", true)]
    [DataRow("Failed", true)]
    [DataRow("Missed", true)]
    [DataRow("Rejected", true)]
    public void AreControlsDisabled_ReflectsCallState(string state, bool expectedDisabled)
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetCallState(state);

        Assert.AreEqual(expectedDisabled, dialog.TestAreControlsDisabled);
    }

    [TestMethod]
    [DataRow("Ringing", false)]
    [DataRow("Connecting", false)]
    [DataRow("Active", false)]
    [DataRow("OnHold", false)]
    [DataRow("Ended", true)]
    [DataRow("Failed", true)]
    [DataRow("Missed", true)]
    [DataRow("Rejected", true)]
    public void IsHangUpDisabled_ReflectsCallState(string state, bool expectedDisabled)
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetCallState(state);

        Assert.AreEqual(expectedDisabled, dialog.TestIsHangUpDisabled);
    }

    // ── Event Callback Tests ────────────────────────────────────────

    [TestMethod]
    public async Task HandleHangUp_InvokesOnHangUpCallback()
    {
        var dialog = new TestableVideoCallDialog();
        var invoked = false;
        var receiver = new object();
        dialog.OnHangUp = EventCallback.Factory.Create(receiver, () => invoked = true);

        await dialog.InvokeHangUp();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task HandleToggleMute_InvokesOnToggleMuteWithValue()
    {
        var dialog = new TestableVideoCallDialog();
        bool? receivedValue = null;
        var receiver = new object();
        dialog.OnToggleMute = EventCallback.Factory.Create<bool>(receiver, val => receivedValue = val);

        await dialog.InvokeToggleMute(true);

        Assert.AreEqual(true, receivedValue);
    }

    [TestMethod]
    public async Task HandleToggleCamera_InvokesOnToggleCameraWithValue()
    {
        var dialog = new TestableVideoCallDialog();
        bool? receivedValue = null;
        var receiver = new object();
        dialog.OnToggleCamera = EventCallback.Factory.Create<bool>(receiver, val => receivedValue = val);

        await dialog.InvokeToggleCamera(false);

        Assert.AreEqual(false, receivedValue);
    }

    [TestMethod]
    public async Task HandleToggleScreenShare_InvokesOnToggleScreenShareWithValue()
    {
        var dialog = new TestableVideoCallDialog();
        bool? receivedValue = null;
        var receiver = new object();
        dialog.OnToggleScreenShare = EventCallback.Factory.Create<bool>(receiver, val => receivedValue = val);

        await dialog.InvokeToggleScreenShare(true);

        Assert.AreEqual(true, receivedValue);
    }

    [TestMethod]
    public async Task HandleMinimize_InvokesOnMinimizeCallback()
    {
        var dialog = new TestableVideoCallDialog();
        var invoked = false;
        var receiver = new object();
        dialog.OnMinimize = EventCallback.Factory.Create(receiver, () => invoked = true);

        await dialog.InvokeMinimize();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task HandlePipClick_InvokesOnPipClickCallback()
    {
        var dialog = new TestableVideoCallDialog();
        var invoked = false;
        var receiver = new object();
        dialog.OnPipClick = EventCallback.Factory.Create(receiver, () => invoked = true);

        await dialog.InvokePipClick();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public void IsCurrentUserHost_WhenHostMatchesCurrentUser_ReturnsTrue()
    {
        var dialog = new TestableVideoCallDialog();
        var userId = Guid.NewGuid();

        dialog.SetCurrentUserAndHost(userId, userId);

        Assert.IsTrue(dialog.TestIsCurrentUserHost);
    }

    [TestMethod]
    public void IsCurrentUserHost_WhenHostDiffersFromCurrentUser_ReturnsFalse()
    {
        var dialog = new TestableVideoCallDialog();

        dialog.SetCurrentUserAndHost(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsFalse(dialog.TestIsCurrentUserHost);
    }

    [TestMethod]
    public async Task HandleAddPeople_InvokesOnAddPeopleCallback()
    {
        var dialog = new TestableVideoCallDialog();
        var invoked = false;
        var receiver = new object();
        dialog.OnAddPeople = EventCallback.Factory.Create(receiver, () => invoked = true);

        await dialog.InvokeAddPeople();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task HandleTransferHost_InvokesOnTransferHostWithTargetUserId()
    {
        var dialog = new TestableVideoCallDialog();
        var targetUserId = Guid.NewGuid();
        Guid? receivedUserId = null;
        var receiver = new object();
        dialog.OnTransferHost = EventCallback.Factory.Create<Guid>(receiver, id => receivedUserId = id);

        await dialog.InvokeTransferHost(targetUserId);

        Assert.AreEqual(targetUserId, receivedUserId);
    }

    [TestMethod]
    public async Task HandleInviteToCall_InvokesOnInviteToCallWithTargetUserId()
    {
        var dialog = new TestableVideoCallDialog();
        var targetUserId = Guid.NewGuid();
        Guid? receivedUserId = null;
        var receiver = new object();
        dialog.OnInviteToCall = EventCallback.Factory.Create<Guid>(receiver, id => receivedUserId = id);

        await dialog.InvokeInviteToCall(targetUserId);

        Assert.AreEqual(targetUserId, receivedUserId);
    }

    [TestMethod]
    public async Task HandleCloseAddPeoplePicker_InvokesOnCloseAddPeoplePickerCallback()
    {
        var dialog = new TestableVideoCallDialog();
        var invoked = false;
        var receiver = new object();
        dialog.OnCloseAddPeoplePicker = EventCallback.Factory.Create(receiver, () => invoked = true);

        await dialog.InvokeCloseAddPeoplePicker();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task HandleAddPeopleSearchInput_InvokesOnAddPeopleSearchChangedWithInputValue()
    {
        var dialog = new TestableVideoCallDialog();
        string? receivedSearchTerm = null;
        var receiver = new object();
        dialog.OnAddPeopleSearchChanged = EventCallback.Factory.Create<string>(receiver, term => receivedSearchTerm = term);

        await dialog.InvokeAddPeopleSearchInput("alice");

        Assert.AreEqual("alice", receivedSearchTerm);
    }

    // ── Waiting Message Tests ───────────────────────────────────────

    [TestMethod]
    public void GetWaitingMessage_WhenRinging_ReturnsWaitingMessage()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetCallState("Ringing");

        Assert.AreEqual("Waiting for others to join...", dialog.TestGetWaitingMessage());
    }

    [TestMethod]
    public void GetWaitingMessage_WhenConnecting_ReturnsEstablishingMessage()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetCallState("Connecting");

        Assert.AreEqual("Establishing connection...", dialog.TestGetWaitingMessage());
    }

    [TestMethod]
    public void GetWaitingMessage_WhenActive_ReturnsEmpty()
    {
        var dialog = new TestableVideoCallDialog();
        dialog.SetCallState("Active");

        Assert.AreEqual(string.Empty, dialog.TestGetWaitingMessage());
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static CallParticipantDto CreateParticipant(string? name = null)
    {
        return new CallParticipantDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DisplayName = name ?? $"User-{Guid.NewGuid().ToString()[..6]}",
            Role = "Participant",
            HasAudio = true,
            HasVideo = true
        };
    }

    private sealed class TestableVideoCallDialog : VideoCallDialog
    {
        public string TestLayoutClass => LayoutClass;
        public string TestGridLayoutName => GridLayoutName;
        public int TestTotalParticipantCount => TotalParticipantCount;
        public bool TestAreControlsDisabled => AreControlsDisabled;
        public bool TestIsHangUpDisabled => IsHangUpDisabled;
        public bool TestIsCurrentUserHost => IsCurrentUserHost;

        public void SetRemoteParticipants(IReadOnlyList<CallParticipantDto> participants)
        {
            RemoteParticipants = participants;
        }

        public void SetCallState(string state)
        {
            CallState = state;
        }

        public void SetCurrentUserAndHost(Guid currentUserId, Guid hostUserId)
        {
            CurrentUserId = currentUserId;
            HostUserId = hostUserId;
        }

        public string TestGetWaitingMessage() => GetWaitingMessage();

        public Task InvokeHangUp() => HandleHangUp();
        public Task InvokeToggleMute(bool val) => HandleToggleMute(val);
        public Task InvokeToggleCamera(bool val) => HandleToggleCamera(val);
        public Task InvokeToggleScreenShare(bool val) => HandleToggleScreenShare(val);
        public Task InvokeAddPeople() => HandleAddPeople();
        public Task InvokeInviteToCall(Guid userId) => HandleInviteToCall(userId);
        public Task InvokeTransferHost(Guid userId) => HandleTransferHost(userId);
        public Task InvokeCloseAddPeoplePicker() => HandleCloseAddPeoplePicker();
        public Task InvokeAddPeopleSearchInput(string term) => HandleAddPeopleSearchInput(new ChangeEventArgs { Value = term });
        public Task InvokeMinimize() => HandleMinimize();
        public Task InvokePipClick() => HandlePipClick();
    }
}
