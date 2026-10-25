using System.Diagnostics;
using System.Text;
using DotNetCloud.Core.AI;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.AI.UI;

/// <summary>Code-behind for the <see cref="AiChatPage"/> component.</summary>
public partial class AiChatPage : ComponentBase
{
    private List<ConversationSummaryDto> _conversations = [];
    private List<MessageDto> _activeMessages = [];
    private ConversationDetailDto? _activeConversation;
    private Guid? _activeConversationId;
    private string _defaultModel = string.Empty;
    private string _userInput = string.Empty;
    private string _streamingContent = string.Empty;
    private string _thinkingContent = string.Empty;
    private bool _hasThinking;
    private string? _errorMessage;
    private bool _isLoading;
    private bool _isStreaming;
    private bool _isModelLoading;
    private bool _isQueued;
    private int _queuePosition;
    private int _queueTotal;
    private bool _ollamaHealthy = true;
    private CallerContext? _caller;
    private ElementReference _messagesContainer;
    private Guid? _copiedMessageId;
    private string? _copiedFormat;
    private CancellationTokenSource? _modelLoadCts;
    private CancellationTokenSource? _streamCts;
    private CancellationTokenSource? _watchdogCts;
    private long _lastChunkTicks;
    private bool _userInitiatedAbort;
    private bool _watchdogArmed;
    private IJSObjectReference? _aiModule;
    private ElementReference _streamScroll;
    private Guid? _renamingConversationId;
    private string _renameTitle = string.Empty;
    private ElementReference _renameTitleInput;

    private static readonly TimeSpan StreamSilenceTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan WatchdogPollInterval = TimeSpan.FromSeconds(2);

    protected override async Task OnInitializedAsync()
    {
        _caller = await GetCallerContextAsync();
        if (_caller is null) return;

        _isLoading = true;
        try
        {
            var userId = _caller.UserId;
            var convsTask = AiApi.ListConversationsAsync(userId, CancellationToken.None);
            var healthTask = AiApi.IsOllamaHealthyAsync(CancellationToken.None);
            var settingsTask = AiApi.GetSettingsAsync(CancellationToken.None);

            await Task.WhenAll(convsTask, healthTask, settingsTask);

            _conversations = [.. await convsTask];
            _ollamaHealthy = await healthTask;
            _defaultModel = (await settingsTask)?.DefaultModel ?? string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize AI assistant page");
            _errorMessage = "Failed to load AI assistant. Check connectivity.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task StartNewConversation()
    {
        if (_caller is null) return;

        _errorMessage = null;
        try
        {
            var conv = await AiApi.CreateConversationAsync(_caller.UserId, "New Chat", null, CancellationToken.None);
            if (conv is null)
            {
                _errorMessage = "Failed to create conversation.";
                return;
            }

            var summary = new ConversationSummaryDto
            {
                Id = conv.Id,
                Title = conv.Title,
                Model = conv.Model,
                CreatedAt = conv.CreatedAt,
                UpdatedAt = conv.UpdatedAt
            };
            _conversations.Insert(0, summary);
            _activeConversation = new ConversationDetailDto
            {
                Id = conv.Id,
                Title = conv.Title,
                Model = conv.Model,
                SystemPrompt = conv.SystemPrompt,
                CreatedAt = conv.CreatedAt,
                UpdatedAt = conv.UpdatedAt,
                Messages = []
            };
            _activeConversationId = conv.Id;
            _activeMessages = [];
            _userInput = string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create conversation");
            _errorMessage = "Failed to create conversation.";
        }
    }

    private void StartRenaming(ConversationSummaryDto conv)
    {
        _renamingConversationId = conv.Id;
        _renameTitle = conv.Title ?? string.Empty;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _aiModule = await JS.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/DotNetCloud.Modules.AI/UI/AiChatPage.razor.js");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load AI chat JS module");
            }
        }

        if (_renamingConversationId is not null)
        {
            try
            {
                await _renameTitleInput.FocusAsync();
            }
            catch
            {
                // Element may not be rendered yet
            }
        }
    }

    private async Task CommitRenameAsync()
    {
        if (_caller is null || _renamingConversationId is null) return;

        var convId = _renamingConversationId.Value;
        var newTitle = _renameTitle.Trim();
        _renamingConversationId = null;

        if (string.IsNullOrWhiteSpace(newTitle)) return;

        try
        {
            var renamed = await AiApi.RenameConversationAsync(_caller.UserId, convId, newTitle, CancellationToken.None);
            if (renamed)
            {
                var conv = _conversations.FirstOrDefault(c => c.Id == convId);
                if (conv is not null)
                {
                    _conversations[_conversations.IndexOf(conv)] = conv with { Title = newTitle };
                }
                if (_activeConversation?.Id == convId)
                {
                    _activeConversation = _activeConversation with { Title = newTitle };
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to rename conversation {Id}", convId);
            _errorMessage = "Failed to rename conversation.";
        }
    }

    private async Task OnRenameTitleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await CommitRenameAsync();
        }
        else if (e.Key == "Escape")
        {
            _renamingConversationId = null;
        }
    }

    private async Task DeleteConversationAsync(Guid conversationId)
    {
        if (_caller is null) return;

        try
        {
            var deleted = await AiApi.DeleteConversationAsync(_caller.UserId, conversationId, CancellationToken.None);
            if (deleted)
            {
                _conversations.RemoveAll(c => c.Id == conversationId);
                if (_activeConversationId == conversationId)
                {
                    _activeConversation = null;
                    _activeConversationId = null;
                    _activeMessages = [];
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete conversation {Id}", conversationId);
            _errorMessage = "Failed to delete conversation.";
        }
    }

    private async Task LoadConversationAsync(Guid conversationId)
    {
        if (_caller is null) return;

        _errorMessage = null;
        try
        {
            var detail = await AiApi.GetConversationAsync(_caller.UserId, conversationId, CancellationToken.None);
            if (detail is null) return;

            _activeConversation = detail;
            _activeConversationId = detail.Id;
            _activeMessages = [.. detail.Messages.OrderBy(m => m.CreatedAt)];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load conversation {Id}", conversationId);
            _errorMessage = "Failed to load conversation.";
        }
    }

    private async Task SendMessageAsync()
    {
        if (_caller is null || _activeConversation is null || string.IsNullOrWhiteSpace(_userInput)) return;

        var userMessage = _userInput.Trim();
        _userInput = string.Empty;
        _errorMessage = null;

        // Add user message to display immediately
        _activeMessages.Add(new MessageDto
        {
            Id = Guid.NewGuid(),
            Role = "user",
            Content = userMessage,
            CreatedAt = DateTime.UtcNow
        });

        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = new CancellationTokenSource();
        var streamToken = _streamCts.Token;
        _userInitiatedAbort = false;
        _watchdogArmed = false;

        _isStreaming = true;
        _isQueued = true;
        _isModelLoading = false;
        _streamingContent = string.Empty;
        _thinkingContent = string.Empty;
        _hasThinking = false;
        StateHasChanged();
        StartStreamWatchdog();

        try
        {
            var fullResponse = new StringBuilder();
            var firstGenerating = true;

            await foreach (var chunk in AiApi.SendMessageStreamingAsync(
                _caller.UserId, _activeConversation.Id, userMessage, streamToken))
            {
                // Any chunk (queue status or content) proves the stream is alive.
                _lastChunkTicks = Stopwatch.GetTimestamp();

                if (streamToken.IsCancellationRequested)
                {
                    // User aborted or watchdog timed out — stop draining the stream.
                    break;
                }

                if (chunk.Status == LlmStreamStatus.Queued)
                {
                    _isQueued = true;
                    _queuePosition = chunk.QueuedPosition ?? _queuePosition;
                    _queueTotal = chunk.QueueTotal ?? _queueTotal;
                    StateHasChanged();
                    continue;
                }

                if (firstGenerating)
                {
                    // Request left the queue and is now generating — start the generating-indicator timer.
                    firstGenerating = false;
                    _isQueued = false;
                    _modelLoadCts?.Cancel();
                    _modelLoadCts = new CancellationTokenSource();
                    var loadToken = _modelLoadCts.Token;
                    _ = DetectModelLoadingAsync(loadToken);
                }

                if (!string.IsNullOrEmpty(chunk.Content) || !string.IsNullOrEmpty(chunk.Thinking))
                {
                    // First real model output has arrived — arm the mid-stream watchdog now.
                    // The server sends a queue "Generating" marker and then goes silent while
                    // Ollama cold-loads the model (gemma4:12b can take >60s to the first token),
                    // so the watchdog must NOT cancel before actual content arrives.
                    _watchdogArmed = true;
                }

                if (!string.IsNullOrEmpty(chunk.Thinking))
                {
                    // Surface the model's live reasoning instead of a stuck spinner.
                    _thinkingContent += chunk.Thinking;
                    _hasThinking = true;
                }

                fullResponse.Append(chunk.Content);
                _streamingContent = fullResponse.ToString();
                StateHasChanged();
                await ScrollChatToBottomAsync();
                await Task.Delay(1); // Yield for UI updates
            }

            if (!streamToken.IsCancellationRequested)
            {
                // Replace streaming with final message
                _activeMessages.Add(new MessageDto
                {
                    Id = Guid.NewGuid(),
                    Role = "assistant",
                    Content = fullResponse.ToString(),
                    CreatedAt = DateTime.UtcNow
                });

                // Update conversation title if first exchange (matches server-side auto-title)
                if (_activeMessages.Count(m => m.Role == "user") == 1)
                {
                    var titlePreview = userMessage.Length > 60 ? userMessage[..60].TrimEnd() + "…" : userMessage;
                    if (_activeConversation is not null)
                    {
                        _activeConversation = _activeConversation with { Title = titlePreview };
                    }
                    var listItem = _conversations.FirstOrDefault(c => c.Id == _activeConversation?.Id);
                    if (listItem is not null)
                    {
                        _conversations[_conversations.IndexOf(listItem)] = listItem with { Title = titlePreview };
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (!_userInitiatedAbort)
            {
                // Watchdog timed the silent stream out — a friendly message was already set.
                Logger.LogWarning("AI stream cancelled by watchdog timeout");
            }
        }
        catch (Exception ex) when (_streamCts?.IsCancellationRequested == true)
        {
            // The gRPC client surfaces an abort/watchdog timeout as
            // RpcException(StatusCode.Cancelled, "Call canceled by the client."),
            // not OperationCanceledException. When the stream CTS was cancelled,
            // treat it as a quiet abort (or the watchdog already set its own
            // timeout message) — never surface the scary "check Ollama" error.
            Logger.LogInformation("AI stream cancelled ({ExceptionType}): {Message}",
                ex.GetType().Name, ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send message");
            _errorMessage = "Failed to get response from AI. Check Ollama connectivity.";
        }
        finally
        {
            StopStreamWatchdog();
            _streamCts?.Dispose();
            _streamCts = null;
            _isStreaming = false;
            _isQueued = false;
            _isModelLoading = false;
            _streamingContent = string.Empty;
            _thinkingContent = string.Empty;
            _hasThinking = false;
            _modelLoadCts?.Cancel();
            StateHasChanged();
        }
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendMessageAsync();
        }
    }

    private async Task<CallerContext?> GetCallerContextAsync()
    {
        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            if (user.Identity?.IsAuthenticated != true) return null;

            var userIdClaim = user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId)) return null;

            var roles = user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray();

            return new CallerContext(userId, roles, CallerType.User);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get caller context");
            return null;
        }
    }

    private async Task DetectModelLoadingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(3000, cancellationToken);
            if (!cancellationToken.IsCancellationRequested && _isStreaming && string.IsNullOrEmpty(_streamingContent) && string.IsNullOrEmpty(_thinkingContent))
            {
                _isModelLoading = true;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when first chunk arrives before the timeout
        }
    }

    /// <summary>
    /// Cancels the in-flight AI request. If it's still queued, it gives up its place in the
    /// queue; if generating, the Ollama call is aborted. The stream loop's cleanup handler
    /// resets the remaining streaming state.
    /// </summary>
    private void AbortStream()
    {
        _userInitiatedAbort = true;
        StopStreamWatchdog();
        try
        {
            _streamCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Stream already finished — nothing to cancel.
        }

        _isStreaming = false;
        _isQueued = false;
        _isModelLoading = false;
        _streamingContent = string.Empty;
        _thinkingContent = string.Empty;
        _hasThinking = false;
        _modelLoadCts?.Cancel();
        StateHasChanged();
    }

    /// <summary>
    /// Starts a background watchdog that cancels the stream if no chunk (queue status or
    /// content) arrives within <see cref="StreamSilenceTimeout"/>. Prevents the chat from
    /// hanging in "Generating…" forever if the server/proxy stream dies silently.
    /// </summary>
    private void StartStreamWatchdog()
    {
        _watchdogCts?.Cancel();
        _watchdogCts?.Dispose();
        _watchdogCts = new CancellationTokenSource();
        var ct = _watchdogCts.Token;
        _lastChunkTicks = Stopwatch.GetTimestamp();

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(WatchdogPollInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var silentFor = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - _lastChunkTicks);
                if (_watchdogArmed && _isStreaming && silentFor > StreamSilenceTimeout)
                {
                    try
                    {
                        _streamCts?.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Stream already cleaned up.
                    }

                    await InvokeAsync(() =>
                    {
                        _errorMessage = "AI stream timed out — no response received. Try again.";
                        StateHasChanged();
                    });
                    break;
                }
            }
        }, ct);
    }

    /// <summary>Stops the stream watchdog.</summary>
    private void StopStreamWatchdog()
    {
        _watchdogCts?.Cancel();
        _watchdogCts?.Dispose();
        _watchdogCts = null;
    }

    /// <summary>
    /// Auto-scrolls the message list (and the streaming region) to the bottom so the
    /// newest generated tokens are always visible.
    /// </summary>
    private async Task ScrollChatToBottomAsync()
    {
        try
        {
            if (_aiModule is not null)
            {
                await _aiModule.InvokeVoidAsync("scrollChatToBottom", _messagesContainer, _streamScroll);
            }
        }
        catch
        {
            // Scroll is best-effort — ignore interop failures (e.g. circuit teardown).
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        StopStreamWatchdog();
        _modelLoadCts?.Cancel();
        _modelLoadCts?.Dispose();
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        if (_aiModule is not null)
        {
            await _aiModule.DisposeAsync();
        }
    }

    private async Task CopyAsMarkdownAsync(string markdownContent)
    {
        try
        {
            await JS.InvokeAsync<bool>("aiClipboard.copyMarkdown", markdownContent);
            var msg = _activeMessages.FirstOrDefault(m => m.Content == markdownContent && m.Role == "assistant");
            if (msg is not null)
            {
                _copiedMessageId = msg.Id;
                _copiedFormat = "md";
                StateHasChanged();
                await Task.Delay(2000);
                _copiedMessageId = null;
                _copiedFormat = null;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to copy markdown to clipboard");
        }
    }

    private async Task CopyAsFormattedAsync(string markdownContent)
    {
        try
        {
            var html = MarkdownRenderer.RenderToHtml(markdownContent);
            await JS.InvokeAsync<bool>("aiClipboard.copyFormatted", html, markdownContent);
            var msg = _activeMessages.FirstOrDefault(m => m.Content == markdownContent && m.Role == "assistant");
            if (msg is not null)
            {
                _copiedMessageId = msg.Id;
                _copiedFormat = "html";
                StateHasChanged();
                await Task.Delay(2000);
                _copiedMessageId = null;
                _copiedFormat = null;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to copy formatted HTML to clipboard");
        }
    }
}
