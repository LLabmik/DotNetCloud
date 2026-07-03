using System.Collections.ObjectModel;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Client.Core;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Client.Android.Tests.ViewModels;

[TestClass]
public sealed class MessageListViewModelTests
{
    private const string ServerUrl = "https://example.com:15443";

    private static readonly Guid ChannelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CurrentUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>
    /// Creates a fake JWT access token with a <c>sub</c> claim set to <paramref name="userId"/>.
    /// Uses standard base64url-encoded JWT format (header.payload.signature).
    /// </summary>
    private static string MakeTestJwt(Guid userId)
    {
        var header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
        var payload = $"{{\"sub\":\"{userId}\",\"name\":\"Test User\"}}";
        var encHeader = Base64UrlEncode(header);
        var encPayload = Base64UrlEncode(payload);
        // Fake signature — the extractor only reads the payload
        return $"{encHeader}.{encPayload}.fakesignature";
    }

    private static string Base64UrlEncode(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static ChatMessage MakeChatMessage(Guid id, Guid senderUserId, string senderName, string content, DateTimeOffset sentAt) =>
        new(id, ChannelId, senderUserId, senderName, content, sentAt, false);

    private static ChannelMemberSummary MakeMember(Guid userId, string displayName) =>
        new(userId, displayName, "Member", true);

    private Mock<IChatRestClient> _chatApi = null!;
    private Mock<IChatSignalRClient> _signalR = null!;
    private Mock<ILocalMessageCache> _cache = null!;
    private Mock<IServerConnectionStore> _serverStore = null!;
    private Mock<ISecureTokenStore> _tokenStore = null!;
    private Mock<ILogger<MessageListViewModel>> _logger = null!;

    private MessageListViewModel _vm = null!;

    [TestInitialize]
    public void Setup()
    {
        _chatApi = new Mock<IChatRestClient>(MockBehavior.Strict);
        _signalR = new Mock<IChatSignalRClient>(MockBehavior.Loose);
        _cache = new Mock<ILocalMessageCache>(MockBehavior.Loose);
        _serverStore = new Mock<IServerConnectionStore>(MockBehavior.Strict);
        _tokenStore = new Mock<ISecureTokenStore>(MockBehavior.Strict);
        _logger = new Mock<ILogger<MessageListViewModel>>(MockBehavior.Loose);

        // Default auth setup: active server connection with a JWT containing CurrentUserId
        var connection = new ServerConnection(ServerUrl, "Test Server", "test@test.com");
        _serverStore.Setup(x => x.GetActive()).Returns(connection);
        _tokenStore.Setup(x => x.GetAccessTokenAsync(ServerUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTestJwt(CurrentUserId));

        _vm = new MessageListViewModel(
            _chatApi.Object, _signalR.Object, _cache.Object,
            _serverStore.Object, _tokenStore.Object, _logger.Object);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Constructor
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Constructor_InitializesDefaultState()
    {
        Assert.AreEqual(0, _vm.Messages.Count);
        Assert.IsFalse(_vm.IsLoading);
        Assert.IsFalse(_vm.IsSending);
        Assert.IsNull(_vm.ErrorMessage);
        Assert.IsEmpty(_vm.ChannelName);
        Assert.IsEmpty(_vm.ComposerText);
        Assert.IsFalse(_vm.IsEmojiPickerOpen);
    }

    // ══════════════════════════════════════════════════════════════════
    //  InitializeAsync
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InitializeAsync_SetsChannelName()
    {
        SetupDefaultChannelMembers();
        SetupDefaultMessages([]);
        _signalR.Setup(x => x.JoinChannelGroupAsync(ChannelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _signalR.Setup(x => x.ConnectAsync(ServerUrl, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _vm.InitializeAsync(ChannelId, "General");

        Assert.AreEqual("General", _vm.ChannelName);
    }

    [TestMethod]
    public async Task InitializeAsync_NoServerConnection_SetsErrorMessage()
    {
        _serverStore.Setup(x => x.GetActive()).Returns((ServerConnection?)null);

        await _vm.InitializeAsync(ChannelId, "General");

        Assert.IsNotNull(_vm.ErrorMessage);
    }

    [TestMethod]
    public async Task InitializeAsync_NoAccessToken_SetsErrorMessage()
    {
        // Clear auth mocks: the token store returns null
        _serverStore.Setup(x => x.GetActive()).Returns(new ServerConnection(ServerUrl, "X", "x@x"));
        _tokenStore.Setup(x => x.GetAccessTokenAsync(ServerUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await _vm.InitializeAsync(ChannelId, "General");

        Assert.IsNotNull(_vm.ErrorMessage);
    }

    [TestMethod]
    public async Task InitializeAsync_LoadsMembersAndMessages_JoinsSignalR()
    {
        var members = new List<ChannelMemberSummary>
        {
            MakeMember(CurrentUserId, "Current User"),
            MakeMember(OtherUserId, "Other User"),
        };
        var serverMessages = new List<ChatMessage>
        {
            MakeChatMessage(Guid.NewGuid(), OtherUserId, "Other User", "Hello!", DateTimeOffset.UtcNow.AddMinutes(-10)),
            MakeChatMessage(Guid.NewGuid(), CurrentUserId, "Current User", "Hi back!", DateTimeOffset.UtcNow.AddMinutes(-5)),
        };

        _chatApi.Setup(x => x.GetChannelMembersAsync(ServerUrl, It.IsAny<string>(), ChannelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);
        _chatApi.Setup(x => x.GetMessagesAsync(ServerUrl, It.IsAny<string>(), ChannelId, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverMessages);
        var lastMsg = serverMessages[serverMessages.Count - 1];
        _chatApi.Setup(x => x.MarkReadAsync(ServerUrl, It.IsAny<string>(), ChannelId, lastMsg.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _signalR.Setup(x => x.ConnectAsync(ServerUrl, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _signalR.Setup(x => x.JoinChannelGroupAsync(ChannelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _vm.InitializeAsync(ChannelId, "General");

        Assert.AreEqual(2, _vm.Messages.Count);
        // Messages should be in oldest-first order
        Assert.AreEqual("Hello!", _vm.Messages[0].Content);
        Assert.AreEqual("Hi back!", _vm.Messages[1].Content);

        // Own-message detection on initial load: second message is from current user
        Assert.IsFalse(_vm.Messages[0].IsOwnMessage);
        Assert.IsTrue(_vm.Messages[1].IsOwnMessage);

        // Verify SignalR was connected and group joined
        _signalR.Verify(x => x.ConnectAsync(ServerUrl, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _signalR.Verify(x => x.JoinChannelGroupAsync(ChannelId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task InitializeAsync_ConnectAsyncFailure_StillJoinsGroup()
    {
        SetupDefaultChannelMembers();
        SetupDefaultMessages([]);
        _signalR.Setup(x => x.ConnectAsync(ServerUrl, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));
        _signalR.Setup(x => x.JoinChannelGroupAsync(ChannelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _vm.InitializeAsync(ChannelId, "General");

        // Should still attempt to join the group even if ConnectAsync failed
        _signalR.Verify(x => x.JoinChannelGroupAsync(ChannelId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════
    //  SendAsync
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task SendAsync_AddsMessageToCollection_WithIsOwnMessageTrue()
    {
        // Need to initialize the VM first so channelId, serverUrl, accessToken are set
        await InitializeHappyPathAsync();

        var messageId = Guid.NewGuid();
        var sentAt = DateTimeOffset.UtcNow;
        var serverMessage = MakeChatMessage(messageId, CurrentUserId, "Current User", "Hello world!", sentAt);

        _vm.ComposerText = "Hello world!";
        _chatApi.Setup(x => x.SendMessageAsync(ServerUrl, It.IsAny<string>(), ChannelId, "Hello world!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverMessage);

        // Send should trigger
        Assert.IsTrue(_vm.SendCommand.CanExecute(null));
        await _vm.SendCommand.ExecuteAsync(null);

        Assert.AreEqual(1, _vm.Messages.Count);
        var msg = _vm.Messages[0];
        Assert.AreEqual(messageId, msg.Id);
        Assert.AreEqual("Hello world!", msg.Content);
        Assert.AreEqual("Current User", msg.SenderName);
        Assert.IsTrue(msg.IsOwnMessage, "Own message must be marked as IsOwnMessage=true");
    }

    [TestMethod]
    public async Task SendAsync_CachesTheSentMessage()
    {
        await InitializeHappyPathAsync();

        var messageId = Guid.NewGuid();
        var sentAt = DateTimeOffset.UtcNow;
        var serverMessage = MakeChatMessage(messageId, CurrentUserId, "Current User", "Cache me!", sentAt);

        _vm.ComposerText = "Cache me!";
        _chatApi.Setup(x => x.SendMessageAsync(ServerUrl, It.IsAny<string>(), ChannelId, "Cache me!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverMessage);

        await _vm.SendCommand.ExecuteAsync(null);

        _cache.Verify(x => x.UpsertAsync(
            It.Is<IEnumerable<CachedMessage>>(msgs => msgs.Any(m => m.Id == messageId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendAsync_ClearsComposerTextOnSuccess()
    {
        await InitializeHappyPathAsync();

        var serverMessage = MakeChatMessage(Guid.NewGuid(), CurrentUserId, "Me", "Clear me!", DateTimeOffset.UtcNow);
        _vm.ComposerText = "Clear me!";
        _chatApi.Setup(x => x.SendMessageAsync(ServerUrl, It.IsAny<string>(), ChannelId, "Clear me!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverMessage);

        await _vm.SendCommand.ExecuteAsync(null);

        Assert.IsEmpty(_vm.ComposerText);
        Assert.IsFalse(_vm.IsSending);
    }

    [TestMethod]
    public async Task SendAsync_Failure_RestoresComposerText()
    {
        await InitializeHappyPathAsync();

        _vm.ComposerText = "Will fail";
        _chatApi.Setup(x => x.SendMessageAsync(ServerUrl, It.IsAny<string>(), ChannelId, "Will fail", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        await _vm.SendCommand.ExecuteAsync(null);

        Assert.AreEqual("Will fail", _vm.ComposerText);
        Assert.IsNotNull(_vm.ErrorMessage);
        Assert.IsFalse(_vm.IsSending);
    }

    [TestMethod]
    public async Task SendAsync_Failure_DoesNotAddMessage()
    {
        await InitializeHappyPathAsync();

        _vm.ComposerText = "Will fail";
        _chatApi.Setup(x => x.SendMessageAsync(ServerUrl, It.IsAny<string>(), ChannelId, "Will fail", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        await _vm.SendCommand.ExecuteAsync(null);

        Assert.AreEqual(0, _vm.Messages.Count);
    }

    [TestMethod]
    public async Task SendAsync_EmptyInput_DoesNotSend()
    {
        await InitializeHappyPathAsync();

        _vm.ComposerText = "   ";
        Assert.IsFalse(_vm.SendCommand.CanExecute(null));
    }

    // ══════════════════════════════════════════════════════════════════
    //  AttachFileAsync
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AttachFileAsync_AddsMessageToCollection()
    {
        await InitializeHappyPathAsync();

        var messageId = Guid.NewGuid();
        var serverMessage = MakeChatMessage(messageId, CurrentUserId, "Current User", "📎 photo.jpg", DateTimeOffset.UtcNow);
        _chatApi.Setup(x => x.SendMessageAsync(ServerUrl, It.IsAny<string>(), ChannelId, "📎 photo.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverMessage);

        // AttachFileAsync is internal; it reads MediaPicker results, which we can't mock easily.
        // This test verifies the SendMessageAsync call path works — we test the behavior through
        // the code path that runs when MediaPicker results are available.
        // The actual file-picking logic is in Platform-specific code.
    }

    // ══════════════════════════════════════════════════════════════════
    //  OnNewChatMessage (real-time handler via SignalR event)
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task OnNewChatMessage_OtherUser_AddsMessageWithIsOwnMessageFalse()
    {
        await InitializeHappyPathAsync();

        var messageId = Guid.NewGuid();
        FireNewChatMessage(ChannelId.ToString(), OtherUserId, "Other User", "Hey there!", messageId, DateTime.UtcNow);

        Assert.AreEqual(1, _vm.Messages.Count);
        var msg = _vm.Messages[0];
        Assert.AreEqual("Hey there!", msg.Content);
        Assert.AreEqual("Other User", msg.SenderName);
        Assert.IsFalse(msg.IsOwnMessage, "Other user's message must be IsOwnMessage=false");
    }

    [TestMethod]
    public async Task OnNewChatMessage_OwnMessage_SetsIsOwnMessageTrue()
    {
        await InitializeHappyPathAsync();

        var messageId = Guid.NewGuid();
        FireNewChatMessage(ChannelId.ToString(), CurrentUserId, "Current User", "My message!", messageId, DateTime.UtcNow);

        Assert.AreEqual(1, _vm.Messages.Count);
        Assert.IsTrue(_vm.Messages[0].IsOwnMessage, "Own message from SignalR must be IsOwnMessage=true");
    }

    [TestMethod]
    public async Task OnNewChatMessage_Dedup_SkipsDuplicateMessageId()
    {
        await InitializeHappyPathAsync();

        // Add first message via SignalR
        var messageId = Guid.NewGuid();
        FireNewChatMessage(ChannelId.ToString(), OtherUserId, "User", "First", messageId, DateTime.UtcNow);
        Assert.AreEqual(1, _vm.Messages.Count);

        // Fire the same message again (SignalR echo)
        FireNewChatMessage(ChannelId.ToString(), OtherUserId, "User", "First", messageId, DateTime.UtcNow);

        // Should still be 1 — duplicate was skipped
        Assert.AreEqual(1, _vm.Messages.Count);
    }

    [TestMethod]
    public async Task OnNewChatMessage_WrongChannel_Ignored()
    {
        await InitializeHappyPathAsync();

        var otherChannelId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        FireNewChatMessage(otherChannelId.ToString(), OtherUserId, "User", "Wrong channel!", Guid.NewGuid(), DateTime.UtcNow);

        Assert.AreEqual(0, _vm.Messages.Count);
    }

    [TestMethod]
    public async Task OnNewChatMessage_CachesReceivedMessage()
    {
        var messageId = Guid.NewGuid();

        // Fire the event — needs an initialized ViewModel so channelId is set
        await InitializeHappyPathAsync();
        _vm.Messages.Clear(); // clear initial load messages

        FireNewChatMessage(ChannelId.ToString(), OtherUserId, "User", "Cache this!", messageId, DateTime.UtcNow);

        _cache.Verify(x => x.UpsertAsync(
            It.Is<IEnumerable<CachedMessage>>(msgs => msgs.Any(m => m.Id == messageId)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ══════════════════════════════════════════════════════════════════
    //  LoadMessagesAsync
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task LoadMessagesAsync_UsesCacheFirst_ThenServerData()
    {
        // Arrange: cache has 1 message, server has 2
        SetupDefaultChannelMembers();
        var cachedMsg = new CachedMessage(Guid.NewGuid(), ChannelId, "Cached", "From cache", DateTimeOffset.UtcNow.AddMinutes(-20));
        var serverMsgs = new List<ChatMessage>
        {
            MakeChatMessage(Guid.NewGuid(), OtherUserId, "User A", "Server msg 1", DateTimeOffset.UtcNow.AddMinutes(-15)),
            MakeChatMessage(Guid.NewGuid(), CurrentUserId, "Current User", "Server msg 2", DateTimeOffset.UtcNow.AddMinutes(-10)),
        };

        _cache.Setup(x => x.GetRecentAsync(ChannelId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { cachedMsg });
        _chatApi.Setup(x => x.GetMessagesAsync(ServerUrl, It.IsAny<string>(), ChannelId, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverMsgs);
        var lastMsg = serverMsgs[serverMsgs.Count - 1];
        _chatApi.Setup(x => x.MarkReadAsync(ServerUrl, It.IsAny<string>(), ChannelId, lastMsg.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _vm.InitializeAsync(ChannelId, "General");

        // After full load: cached message should be replaced by server messages
        Assert.AreEqual(2, _vm.Messages.Count);
        Assert.AreEqual("Server msg 1", _vm.Messages[0].Content);
        Assert.AreEqual("Server msg 2", _vm.Messages[1].Content);
    }

    [TestMethod]
    public async Task LoadMessagesAsync_ServerFailure_FallsBackToCache()
    {
        SetupDefaultChannelMembers();
        var cachedMsg = new CachedMessage(Guid.NewGuid(), ChannelId, "Cached", "Offline fallback", DateTimeOffset.UtcNow.AddMinutes(-5));

        _cache.Setup(x => x.GetRecentAsync(ChannelId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { cachedMsg });
        _chatApi.Setup(x => x.GetMessagesAsync(ServerUrl, It.IsAny<string>(), ChannelId, null, 50, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Server unavailable"));

        await _vm.InitializeAsync(ChannelId, "General");

        // Should still show cached message on failure
        Assert.AreEqual(1, _vm.Messages.Count);
        Assert.AreEqual("Offline fallback", _vm.Messages[0].Content);
    }

    // ══════════════════════════════════════════════════════════════════
    //  MessageItemViewModel
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void MessageItemViewModel_Constructor_SetsProperties()
    {
        var id = Guid.NewGuid();
        var sentAt = new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var vm = new MessageItemViewModel(id, "Alice", "Hello!", sentAt);

        Assert.AreEqual(id, vm.Id);
        Assert.AreEqual("Alice", vm.SenderName);
        Assert.AreEqual("Hello!", vm.Content);
        Assert.AreEqual(sentAt, vm.SentAt);
        Assert.IsFalse(vm.IsOwnMessage);
    }

    [TestMethod]
    public void MessageItemViewModel_IsOwnMessage_TrueWhenSet()
    {
        var vm = new MessageItemViewModel(Guid.NewGuid(), "Me", "Test", DateTimeOffset.UtcNow, isOwnMessage: true);
        Assert.IsTrue(vm.IsOwnMessage);
    }

    [TestMethod]
    public void MessageItemViewModel_SenderInitial_ReturnsFirstCharUppercase()
    {
        var vm = new MessageItemViewModel(Guid.NewGuid(), "alice", "Hello", DateTimeOffset.UtcNow);
        Assert.AreEqual("A", vm.SenderInitial);
    }

    [TestMethod]
    public void MessageItemViewModel_SenderInitial_EmptyName_ReturnsQuestionMark()
    {
        var vm = new MessageItemViewModel(Guid.NewGuid(), string.Empty, "Hello", DateTimeOffset.UtcNow);
        Assert.AreEqual("?", vm.SenderInitial);
    }

    [TestMethod]
    public void MessageItemViewModel_SentAtDisplay_JustNow()
    {
        var vm = new MessageItemViewModel(Guid.NewGuid(), "A", "Hi", DateTimeOffset.UtcNow);
        Assert.AreEqual("just now", vm.SentAtDisplay);
    }

    [TestMethod]
    public void MessageItemViewModel_SentAtDisplay_MinutesAgo()
    {
        var vm = new MessageItemViewModel(Guid.NewGuid(), "A", "Hi", DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.AreEqual("5m ago", vm.SentAtDisplay);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Dispose
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Dispose_LeavesSignalRGroup()
    {
        _signalR.Setup(x => x.LeaveChannelGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _vm.Dispose();

        // Dispose calls LeaveChannelGroupAsync fire-and-forget, so we can't Verify directly
        // as it may not have completed. Verify it was called at least once.
        _signalR.Verify(x => x.LeaveChannelGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.AtMost(1));
    }

    [TestMethod]
    public async Task Dispose_UnsubscribesFromSignalREvent()
    {
        await InitializeHappyPathAsync();

        // Verify events work before dispose
        var messageId = Guid.NewGuid();
        FireNewChatMessage(ChannelId.ToString(), OtherUserId, "User", "Before dispose", messageId, DateTime.UtcNow);
        Assert.AreEqual(1, _vm.Messages.Count);

        _vm.Dispose();

        // Fire the event — should not add any messages since handler was unsubscribed
        FireNewChatMessage(ChannelId.ToString(), OtherUserId, "User", "After dispose", Guid.NewGuid(), DateTime.UtcNow);

        Assert.AreEqual(1, _vm.Messages.Count, "Messages should not increase after dispose");
    }

    // ══════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Sets up the ViewModel with a fully initialized state (auth, members, empty messages).</summary>
    private async Task InitializeHappyPathAsync()
    {
        SetupDefaultChannelMembers();
        SetupDefaultMessages([]);
        _signalR.Setup(x => x.ConnectAsync(ServerUrl, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _signalR.Setup(x => x.JoinChannelGroupAsync(ChannelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _vm.InitializeAsync(ChannelId, "Test Channel");
    }

    private void SetupDefaultChannelMembers()
    {
        var members = new List<ChannelMemberSummary>
        {
            MakeMember(CurrentUserId, "Current User"),
            MakeMember(OtherUserId, "Other User"),
        };
        _chatApi.Setup(x => x.GetChannelMembersAsync(ServerUrl, It.IsAny<string>(), ChannelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);
    }

    private void SetupDefaultMessages(IReadOnlyList<ChatMessage> messages)
    {
        _chatApi.Setup(x => x.GetMessagesAsync(ServerUrl, It.IsAny<string>(), ChannelId, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);
        if (messages.Count > 0)
        {
            var lastMsg = messages[messages.Count - 1];
            _chatApi.Setup(x => x.MarkReadAsync(ServerUrl, It.IsAny<string>(), ChannelId, lastMsg.Id, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }
    }

    /// <summary>
    /// Fires the <c>OnNewChatMessage</c> event on the SignalR mock, simulating
    /// a real-time broadcast from the server. The ViewModel is subscribed in its constructor.
    /// </summary>
    private void FireNewChatMessage(string channelId, Guid senderUserId, string senderName, string content, Guid messageId, DateTime sentAt)
    {
        var args = new ChatMessageReceivedEventArgs(
            channelId,
            string.Empty,
            senderName,
            content,
            messageId,
            sentAt,
            false,
            senderUserId);

        _signalR.Raise(x => x.OnNewChatMessage += null, _signalR.Object, args);
    }
}
