using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.SyncTray.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.SyncTray.Tests.ViewModels;

[TestClass]
public sealed class QuickReplyViewModelTests
{
    private const string TestChannelId = "11111111-1111-1111-1111-111111111111";
    private const string TestChannelName = "general";
    private const string TestServerUrl = "https://cloud.example.com";

    // ── Constructor behaviour ─────────────────────────────────────────────

    [TestMethod]
    public void Constructor_SetsChannelName()
    {
        var (vm, _) = BuildVm();
        Assert.AreEqual(TestChannelName, vm.ChannelName);
    }

    [TestMethod]
    public void Constructor_EmptyChannelName_FallsBackToChat()
    {
        var chatApi = new Mock<IChatApiClient>();
        var vm = new QuickReplyViewModel(
            TestChannelId, string.Empty, TestServerUrl,
            chatApi.Object, NullLogger<QuickReplyViewModel>.Instance);

        Assert.AreEqual("Chat", vm.ChannelName);
    }

    [TestMethod]
    public void Constructor_MessageTextEmpty_CanSendIsFalse()
    {
        var (vm, _) = BuildVm();
        Assert.IsFalse(vm.CanSend);
    }

    // ── CanSend property ──────────────────────────────────────────────────

    [TestMethod]
    public void MessageText_NonEmpty_CanSendIsTrue()
    {
        var (vm, _) = BuildVm();
        vm.MessageText = "Hello";
        Assert.IsTrue(vm.CanSend);
    }

    [TestMethod]
    public void MessageText_WhitespaceOnly_CanSendIsFalse()
    {
        var (vm, _) = BuildVm();
        vm.MessageText = "   ";
        Assert.IsFalse(vm.CanSend);
    }

    // ── Send success ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task SendCommand_Success_RaisesCloseRequestedAndClearsText()
    {
        var (vm, chatApi) = BuildVm();
        chatApi.Setup(a => a.SendMessageAsync(TestServerUrl, null,
            Guid.Parse(TestChannelId), "Hello", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        bool closeRaised = false;
        vm.CloseRequested += (_, _) => closeRaised = true;

        vm.MessageText = "Hello";
        await ExecuteSendAsync(vm);

        Assert.IsTrue(closeRaised, "CloseRequested should be raised on success.");
        Assert.AreEqual(string.Empty, vm.MessageText);
        Assert.IsNull(vm.ErrorMessage);
    }

    [TestMethod]
    public async Task SendCommand_Success_CallsSendMessageWithCorrectArgs()
    {
        var (vm, chatApi) = BuildVm();
        chatApi.Setup(a => a.SendMessageAsync(It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        vm.MessageText = "Test message";
        await ExecuteSendAsync(vm);

        chatApi.Verify(a => a.SendMessageAsync(
            TestServerUrl,
            null,
            Guid.Parse(TestChannelId),
            "Test message",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Send failure ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task SendCommand_Failure_SetsErrorMessageAndDoesNotClose()
    {
        var (vm, chatApi) = BuildVm();
        chatApi.Setup(a => a.SendMessageAsync(It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        bool closeRaised = false;
        vm.CloseRequested += (_, _) => closeRaised = true;

        vm.MessageText = "Hello";
        await ExecuteSendAsync(vm);

        Assert.IsFalse(closeRaised, "CloseRequested must NOT be raised on failure.");
        Assert.IsNotNull(vm.ErrorMessage);
        StringAssert.Contains(vm.ErrorMessage, "Failed to send");
    }

    [TestMethod]
    public async Task SendCommand_Failure_IsSendingReturnsFalse()
    {
        var (vm, chatApi) = BuildVm();
        chatApi.Setup(a => a.SendMessageAsync(It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("server error"));

        vm.MessageText = "Hello";
        await ExecuteSendAsync(vm);

        Assert.IsFalse(vm.IsSending);
    }

    // ── Empty message guard ───────────────────────────────────────────────

    [TestMethod]
    public async Task SendCommand_EmptyMessage_DoesNotCallApi()
    {
        var (vm, chatApi) = BuildVm();
        vm.MessageText = string.Empty;

        await ExecuteSendAsync(vm);

        chatApi.Verify(a => a.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Cancel ────────────────────────────────────────────────────────────

    [TestMethod]
    public void CancelCommand_RaisesCloseRequested()
    {
        var (vm, _) = BuildVm();

        bool closeRaised = false;
        vm.CloseRequested += (_, _) => closeRaised = true;

        vm.CancelCommand.Execute(null);

        Assert.IsTrue(closeRaised);
    }

    // ── Typing indicator ──────────────────────────────────────────────────

    [TestMethod]
    public async Task MessageText_NonEmpty_EventuallyCallsNotifyTypingAsync()
    {
        var (vm, chatApi) = BuildVm();
        chatApi.Setup(a => a.NotifyTypingAsync(It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        vm.MessageText = "Hello";

        // Wait a bit over debounce period (500 ms).
        await Task.Delay(700);

        chatApi.Verify(a => a.NotifyTypingAsync(
            TestServerUrl, null, Guid.Parse(TestChannelId), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task MessageText_RapidChanges_CallsNotifyTypingOnce()
    {
        var (vm, chatApi) = BuildVm();
        int callCount = 0;
        chatApi.Setup(a => a.NotifyTypingAsync(It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .Returns(Task.CompletedTask);

        // Rapid keystrokes within 500 ms debounce window.
        vm.MessageText = "H";
        await Task.Delay(50);
        vm.MessageText = "He";
        await Task.Delay(50);
        vm.MessageText = "Hel";
        await Task.Delay(50);
        vm.MessageText = "Hell";
        await Task.Delay(50);
        vm.MessageText = "Hello";

        // Wait for debounce to fire once.
        await Task.Delay(700);

        // Only one NotifyTypingAsync call for the settled message.
        Assert.AreEqual(1, callCount);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static (QuickReplyViewModel vm, Mock<IChatApiClient> chatApi) BuildVm()
    {
        var chatApi = new Mock<IChatApiClient>();
        var vm = new QuickReplyViewModel(
            TestChannelId,
            TestChannelName,
            TestServerUrl,
            chatApi.Object,
            NullLogger<QuickReplyViewModel>.Instance);
        return (vm, chatApi);
    }

    /// <summary>
    /// Executes the SendCommand and waits for the async work to complete.
    /// </summary>
    private static async Task ExecuteSendAsync(QuickReplyViewModel vm)
    {
        // SendCommand is an AsyncRelayCommand whose Execute wraps an async Task.
        // We invoke it and then yield briefly to let the task complete.
        vm.SendCommand.Execute(null);
        await Task.Delay(100); // Let the async task propagate and complete.
    }
}
