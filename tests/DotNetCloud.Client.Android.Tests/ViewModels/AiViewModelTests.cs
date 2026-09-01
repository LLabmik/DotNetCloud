using DotNetCloud.Client.Android.Ai;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Client.Core;
using Microsoft.Maui.ApplicationModel;
using Moq;

namespace DotNetCloud.Client.Android.Tests.ViewModels;

[TestClass]
public sealed class AiViewModelTests
{
    private const string ServerUrl = "https://example.com:15443";
    private const string Token = "test-access-token";

    private static readonly Guid ConversationId = Guid.NewGuid();

    private Mock<IAiRestClient> _ai = null!;
    private Mock<IServerConnectionStore> _serverStore = null!;
    private Mock<ISecureTokenStore> _tokenStore = null!;
    private Mock<ITokenRefreshService> _tokenRefresh = null!;
    private Mock<IClipboard> _clipboard = null!;

    private AiViewModel _vm = null!;

    [TestInitialize]
    public void Setup()
    {
        _ai = new Mock<IAiRestClient>(MockBehavior.Loose);
        _serverStore = new Mock<IServerConnectionStore>(MockBehavior.Strict);
        _tokenStore = new Mock<ISecureTokenStore>(MockBehavior.Strict);
        _tokenRefresh = new Mock<ITokenRefreshService>(MockBehavior.Strict);
        _clipboard = new Mock<IClipboard>(MockBehavior.Loose);
        _clipboard.Setup(x => x.SetTextAsync(It.IsAny<string?>())).Returns(Task.CompletedTask);

        var connection = new ServerConnection(ServerUrl, "Test Server", "test@test.com");
        _serverStore.Setup(x => x.GetActive()).Returns(connection);
        _tokenRefresh.Setup(x => x.EnsureFreshAccessTokenAsync(ServerUrl, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(Token);
        _tokenStore.Setup(x => x.GetAccessTokenAsync(ServerUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Token);

        _vm = new AiViewModel(_ai.Object, _serverStore.Object, _tokenStore.Object, _tokenRefresh.Object, _clipboard.Object);
    }

    // ── Initial state ──────────────────────────────────────────────────

    [TestMethod]
    public void Constructor_InitializesDefaultState()
    {
        Assert.IsTrue(_vm.ShowConversationList);
        Assert.IsFalse(_vm.IsLoading);
        Assert.IsFalse(_vm.IsStreaming);
        Assert.IsTrue(_vm.OllamaHealthy);
        Assert.AreEqual(0, _vm.Conversations.Count);
        Assert.AreEqual(0, _vm.ActiveMessages.Count);
        Assert.AreEqual("", _vm.DefaultModel);
        Assert.IsNull(_vm.ActiveConversationId);
        Assert.AreEqual("", _vm.ComposerText);
        Assert.IsNull(_vm.ErrorMessage);
    }

    // ── LoadAsync ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task LoadAsync_PopulatesConversationsAndDefaultModel()
    {
        _ai.Setup(x => x.ListConversationsAsync(ServerUrl, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AiConversationDto>
            {
                new() { Id = ConversationId, Title = "Existing", Model = "gpt-oss:20b", UpdatedAt = DateTime.UtcNow }
            });
        _ai.Setup(x => x.GetOllamaHealthAsync(ServerUrl, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _ai.Setup(x => x.GetSettingsAsync(ServerUrl, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSettingsDto { DefaultModel = "gpt-oss:20b", Provider = "ollama" });

        await _vm.LoadAsync();

        Assert.AreEqual(1, _vm.Conversations.Count);
        Assert.AreEqual("gpt-oss:20b", _vm.DefaultModel);
        Assert.IsFalse(_vm.OllamaHealthy);
        Assert.IsFalse(_vm.IsLoading);
        Assert.IsTrue(_vm.ShowConversationList);
    }

    [TestMethod]
    public async Task LoadAsync_NoConnection_SetsError()
    {
        _serverStore.Setup(x => x.GetActive()).Returns((ServerConnection?)null);

        await _vm.LoadAsync();

        Assert.IsNotNull(_vm.ErrorMessage);
        Assert.IsFalse(_vm.IsLoading);
    }

    [TestMethod]
    public async Task LoadAsync_ProviderUnreachable_StillLoadsConversations()
    {
        // Settings call fails (server → Ollama unreachable, HTTP 500) but conversations
        // are DB-backed and must still load; the Ollama banner should show instead.
        _ai.Setup(x => x.GetSettingsAsync(ServerUrl, Token, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("500 (Internal Server Error)"));
        _ai.Setup(x => x.GetOllamaHealthAsync(ServerUrl, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _ai.Setup(x => x.ListConversationsAsync(ServerUrl, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AiConversationDto>
            {
                new() { Id = ConversationId, Title = "Existing", Model = "gpt-oss:20b", UpdatedAt = DateTime.UtcNow }
            });

        await _vm.LoadAsync();

        Assert.AreEqual(1, _vm.Conversations.Count);
        Assert.AreEqual("", _vm.DefaultModel);
        Assert.IsFalse(_vm.OllamaHealthy);
        Assert.IsFalse(_vm.IsLoading);
        Assert.IsNotNull(_vm.ErrorMessage);
    }

    // ── NewConversationAsync ──────────────────────────────────────────

    [TestMethod]
    public async Task NewConversationAsync_CreatesAndShowsChat()
    {
        _ai.Setup(x => x.CreateConversationAsync(ServerUrl, Token, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiConversationDto { Id = ConversationId, Title = "New Chat", Model = "gpt-oss:20b" });

        await _vm.NewConversationCommand.ExecuteAsync(null);

        Assert.AreEqual(1, _vm.Conversations.Count);
        Assert.AreEqual(ConversationId, _vm.ActiveConversationId);
        Assert.IsFalse(_vm.ShowConversationList);
        Assert.AreEqual("New Chat", _vm.ActiveConversationTitle);
    }

    // ── SelectConversationAsync ───────────────────────────────────────

    [TestMethod]
    public async Task SelectConversationAsync_LoadsMessages()
    {
        var message = new AiMessageDto { Id = Guid.NewGuid(), Role = "user", Content = "hello" };
        _ai.Setup(x => x.GetConversationAsync(ServerUrl, Token, ConversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiConversationDto
            {
                Id = ConversationId,
                Title = "Existing",
                Model = "gpt-oss:20b",
                Messages = new List<AiMessageDto> { message }
            });

        await _vm.SelectConversationCommand.ExecuteAsync(new AiConversationDto
        {
            Id = ConversationId,
            Title = "Existing",
            Model = "gpt-oss:20b"
        });

        Assert.AreEqual(1, _vm.ActiveMessages.Count);
        Assert.AreEqual("hello", _vm.ActiveMessages[0].Content);
        Assert.IsFalse(_vm.ShowConversationList);
    }

    // ── SendMessageAsync ──────────────────────────────────────────────

    [TestMethod]
    public async Task SendMessageAsync_AppendsUserMessage_ThenStreamsAssistantReply()
    {
        _ai.Setup(x => x.CreateConversationAsync(ServerUrl, Token, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiConversationDto { Id = ConversationId, Title = "New Chat", Model = "gpt-oss:20b" });
        _ai.Setup(x => x.SendMessageStreamingAsync(ServerUrl, Token, ConversationId, "hello", It.IsAny<CancellationToken>()))
            .Returns(StreamChunks(("Hi ", false), ("there", false), ("", true)));
        _ai.Setup(x => x.ListConversationsAsync(ServerUrl, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AiConversationDto>
            {
                new() { Id = ConversationId, Title = "New Chat", Model = "gpt-oss:20b", UpdatedAt = DateTime.UtcNow }
            });

        await _vm.NewConversationCommand.ExecuteAsync(null);
        _vm.ComposerText = "hello";
        await _vm.SendMessageCommand.ExecuteAsync(null);

        Assert.IsFalse(_vm.IsStreaming);
        // user message + assistant reply
        Assert.AreEqual(2, _vm.ActiveMessages.Count);
        Assert.AreEqual("user", _vm.ActiveMessages[0].Role);
        Assert.AreEqual("hello", _vm.ActiveMessages[0].Content);
        Assert.AreEqual("assistant", _vm.ActiveMessages[1].Role);
        Assert.AreEqual("Hi there", _vm.ActiveMessages[1].Content);
        Assert.AreEqual("", _vm.ComposerText);
    }

    [TestMethod]
    public async Task SendMessageAsync_NoActiveConversation_DoesNothing()
    {
        _vm.ComposerText = "hello";
        await _vm.SendMessageCommand.ExecuteAsync(null);

        Assert.AreEqual(0, _vm.ActiveMessages.Count);
        _ai.Verify(x => x.SendMessageStreamingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SendMessageAsync_QueuedChunk_SetsQueueStatus()
    {
        AiViewModel.ModelLoadDelay = TimeSpan.FromMilliseconds(100);
        try
        {
            _vm.Conversations.Add(new AiConversationDto { Id = ConversationId, Title = "Test", Model = "gpt-oss:20b" });
            _vm.ActiveConversationId = ConversationId;
            _vm.ComposerText = "hello";

            _ai.Setup(x => x.SendMessageStreamingAsync(ServerUrl, Token, ConversationId, "hello", It.IsAny<CancellationToken>()))
                .Returns(QueuedThenGeneratingStream());

            var sendTask = _vm.SendMessageCommand.ExecuteAsync(null);

            // While the request is queued, the live queue status is surfaced.
            await Task.Delay(TimeSpan.FromMilliseconds(120));
            Assert.IsTrue(_vm.IsQueued, "Request should show as queued before generation starts.");
            Assert.AreEqual(3, _vm.QueuePosition);
            Assert.AreEqual(8, _vm.QueueTotal);
            Assert.AreEqual("In queue: position 3 of 8", _vm.QueueStatusText);

            await sendTask;

            Assert.IsFalse(_vm.IsQueued, "Queue status should clear once generation completes.");
            Assert.AreEqual(2, _vm.ActiveMessages.Count); // user + assistant
            Assert.AreEqual("Hello", _vm.ActiveMessages[1].Content);
        }
        finally
        {
            AiViewModel.ModelLoadDelay = TimeSpan.FromSeconds(3);
        }
    }

    [TestMethod]
    public async Task SendMessageAsync_ThinkingChunks_SurfaceReasoning()
    {
        AiViewModel.ModelLoadDelay = TimeSpan.FromMilliseconds(100);
        try
        {
            _vm.Conversations.Add(new AiConversationDto { Id = ConversationId, Title = "Test", Model = "gemma4:12b" });
            _vm.ActiveConversationId = ConversationId;
            _vm.ComposerText = "blue silver";

            _ai.Setup(x => x.SendMessageStreamingAsync(ServerUrl, Token, ConversationId, "blue silver", It.IsAny<CancellationToken>()))
                .Returns(ThinkingThenContentStream());

            var sendTask = _vm.SendMessageCommand.ExecuteAsync(null);

            // While the model is thinking, the reasoning text is surfaced live.
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            Assert.IsTrue(_vm.HasThinking, "Thinking block should be visible while reasoning.");
            Assert.AreEqual("The user asks about blue silver. I should think about this carefully.", _vm.StreamingThinking);
            Assert.IsFalse(_vm.IsModelLoading, "Model-load indicator must not show while thinking is visible.");

            await sendTask;

            Assert.IsFalse(_vm.HasThinking, "Thinking state should clear once streaming completes.");
            Assert.AreEqual(2, _vm.ActiveMessages.Count); // user + assistant
            Assert.AreEqual("Blue silver is the answer.", _vm.ActiveMessages[1].Content);
        }
        finally
        {
            AiViewModel.ModelLoadDelay = TimeSpan.FromSeconds(3);
        }
    }

    // ── DeleteConversationAsync ───────────────────────────────────────

    [TestMethod]
    public async Task DeleteConversationAsync_RemovesConversation()
    {
        var conversation = new AiConversationDto { Id = ConversationId, Title = "Old", Model = "gpt-oss:20b" };
        _vm.Conversations.Add(conversation);
        _ai.Setup(x => x.DeleteConversationAsync(ServerUrl, Token, ConversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _vm.DeleteConversationCommand.ExecuteAsync(conversation);

        Assert.AreEqual(0, _vm.Conversations.Count);
    }

    // ── CommitRenameAsync ─────────────────────────────────────────────

    [TestMethod]
    public async Task BeginRename_RaisesRenameRequested()
    {
        var conversation = new AiConversationDto { Id = ConversationId, Title = "Old", Model = "gpt-oss:20b" };
        AiConversationDto? raised = null;
        _vm.RenameRequested += c => raised = c;

        _vm.BeginRenameCommand.Execute(conversation);

        Assert.IsNotNull(raised);
        Assert.AreEqual(ConversationId, raised!.Id);
    }

    [TestMethod]
    public async Task CommitRenameAsync_UpdatesConversationTitle()
    {
        var conversation = new AiConversationDto { Id = ConversationId, Title = "Old", Model = "gpt-oss:20b" };
        _vm.Conversations.Add(conversation);
        _vm.BeginRenameCommand.Execute(conversation);

        _ai.Setup(x => x.RenameConversationAsync(ServerUrl, Token, ConversationId, "New title", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _vm.CommitRenameAsync("New title");

        Assert.AreEqual("New title", _vm.Conversations[0].Title);
    }

    // ── Copy message ──────────────────────────────────────────────────

    [TestMethod]
    public async Task CopyMessageAsync_CopiesContentToClipboard_AndSetsCopiedState()
    {
        var message = new AiMessageDto { Id = Guid.NewGuid(), Role = "assistant", Content = "Hello **world**" };

        await _vm.CopyMessageCommand.ExecuteAsync(message);

        _clipboard.Verify(x => x.SetTextAsync("Hello **world**"), Times.Once);
        Assert.AreEqual(message.Id, _vm.CopiedMessageId);
    }

    [TestMethod]
    public async Task CopyMessageAsync_NullOrEmptyContent_DoesNothing()
    {
        await _vm.CopyMessageCommand.ExecuteAsync(null);
        await _vm.CopyMessageCommand.ExecuteAsync(new AiMessageDto { Id = Guid.NewGuid(), Role = "assistant", Content = "" });

        _clipboard.Verify(x => x.SetTextAsync(It.IsAny<string?>()), Times.Never);
        Assert.IsNull(_vm.CopiedMessageId);
    }

    // ── Model-loading indicator ───────────────────────────────────────

    [TestMethod]
    public async Task SendMessageAsync_ShowsModelLoading_WhileWaitingForFirstChunk()
    {
        AiViewModel.ModelLoadDelay = TimeSpan.FromMilliseconds(50);
        try
        {
            _vm.Conversations.Add(new AiConversationDto { Id = ConversationId, Title = "Test", Model = "gpt-oss:20b" });
            _vm.ActiveConversationId = ConversationId;
            _vm.ComposerText = "hello";

            // Queued → generating marker (no tokens yet) → content after a long gap.
            _ai.Setup(x => x.SendMessageStreamingAsync(ServerUrl, Token, ConversationId, "hello", It.IsAny<CancellationToken>()))
                .Returns(DelayedFirstChunkStream());

            var sendTask = _vm.SendMessageCommand.ExecuteAsync(null);

            // While queued, the model-load timer must NOT have started.
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.IsTrue(_vm.IsQueued, "Request should be queued before the generating marker.");
            Assert.IsFalse(_vm.IsModelLoading, "Model-load indicator must not start while queued.");

            // Once generating and no tokens have arrived, the loading indicator shows.
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            Assert.IsTrue(_vm.IsModelLoading, "Loading indicator should show once generating, before tokens arrive.");
            Assert.IsFalse(_vm.IsQueued, "Queue status should clear once generation starts.");

            await sendTask;
            Assert.IsFalse(_vm.IsModelLoading, "Loading indicator should clear once streaming completes.");
            Assert.AreEqual(2, _vm.ActiveMessages.Count); // user + assistant
        }
        finally
        {
            AiViewModel.ModelLoadDelay = TimeSpan.FromSeconds(3);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static async IAsyncEnumerable<AiStreamChunk> StreamChunks(params (string Content, bool Done)[] chunks)
    {
        foreach (var (content, done) in chunks)
        {
            yield return new AiStreamChunk { Content = content, Done = done };
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<AiStreamChunk> DelayedFirstChunkStream()
    {
        yield return new AiStreamChunk { Status = "queued", Position = 1, Total = 1, Content = "", Done = false };
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        yield return new AiStreamChunk { Status = "generating", Content = "", Done = false };
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        yield return new AiStreamChunk { Content = "Hello there!", Done = false };
        await Task.Yield();
        yield return new AiStreamChunk { Content = "", Done = true };
    }

    private static async IAsyncEnumerable<AiStreamChunk> QueuedThenGeneratingStream()
    {
        yield return new AiStreamChunk { Status = "queued", Position = 3, Total = 8, Content = "", Done = false };
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        yield return new AiStreamChunk { Status = "generating", Content = "Hello", Done = false };
        await Task.Yield();
        yield return new AiStreamChunk { Status = "done", Content = "", Done = true };
    }

    private static async IAsyncEnumerable<AiStreamChunk> ThinkingThenContentStream()
    {
        yield return new AiStreamChunk { Status = "generating", Content = "", Thinking = "The user asks about blue silver. ", Done = false };
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        yield return new AiStreamChunk { Status = "generating", Content = "", Thinking = "I should think about this carefully.", Done = false };
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        yield return new AiStreamChunk { Content = "Blue silver is the answer.", Done = false };
        await Task.Yield();
        yield return new AiStreamChunk { Content = "", Done = true };
    }
}
