using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Ai;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// ViewModel for the AI Assistant tab. Handles the conversation list, model selection,
/// streaming chat, delete, rename, and copying messages.
/// </summary>
public sealed partial class AiViewModel : ObservableObject
{
    private readonly IAiRestClient _ai;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ITokenRefreshService _tokenRefresh;
    private readonly IClipboard _clipboard;

    private CancellationTokenSource? _streamCts;
    private CancellationTokenSource? _modelLoadCts;
    private AiConversationDto? _renameTarget;

    /// <summary>
    /// Window before "Loading model into memory…" is shown while waiting for the first
    /// stream chunk. Internal + settable so tests can shorten it.
    /// </summary>
    internal static TimeSpan ModelLoadDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Fired when the user requests a rename; the page shows a prompt and calls <see cref="CommitRenameAsync"/>.</summary>
    public event Action<AiConversationDto>? RenameRequested;

    /// <summary>
    /// Fired when the active chat content changes (new message or stream chunk);
    /// the page uses this to keep the message list scrolled to the bottom.
    /// </summary>
    public event Action? ScrollRequested;

    public AiViewModel(
        IAiRestClient ai,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ITokenRefreshService tokenRefresh,
        IClipboard clipboard)
    {
        _ai = ai;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _tokenRefresh = tokenRefresh;
        _clipboard = clipboard;
    }

    /// <summary>
    /// Safely dispatches an action to the main thread. In test context (portable MAUI assemblies),
    /// <see cref="MainThread.BeginInvokeOnMainThread"/> throws <see cref="NotImplementedException"/>;
    /// this wrapper silently swallows that exception so the ViewModel remains testable.
    /// </summary>
    private static void Dispatch(Action action)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
        catch (NotImplementedException)
        {
            // Portable assembly fallback — execute inline in test context
            action();
        }
    }

    private async Task<(string? serverUrl, string? token)> GetCredentialsAsync()
    {
        var conn = _serverStore.GetActive();
        if (conn is null)
            return (null, null);

        var token = await _tokenRefresh.EnsureFreshAccessTokenAsync(conn.ServerBaseUrl);
        if (string.IsNullOrWhiteSpace(token))
            token = await _tokenStore.GetAccessTokenAsync(conn.ServerBaseUrl);

        return (conn.ServerBaseUrl, token);
    }

    // ── Observable state ──────────────────────────────────────────────

    /// <summary>The list of conversations for the current user.</summary>
    public ObservableCollection<AiConversationDto> Conversations { get; } = [];

    /// <summary>Messages of the currently active conversation.</summary>
    public ObservableCollection<AiMessageDto> ActiveMessages { get; } = [];

    [ObservableProperty]
    private Guid? _activeConversationId;

    /// <summary>The admin-configured default model, shown as static text.</summary>
    [ObservableProperty]
    private string _defaultModel = "";

    /// <summary>The model of the active conversation, shown in the chat header.</summary>
    [ObservableProperty]
    private string _activeConversationModel = "";

    /// <summary>True while the request is waiting in the inference queue.</summary>
    [ObservableProperty]
    private bool _isQueued;

    /// <summary>Current 1-based queue position while queued.</summary>
    [ObservableProperty]
    private int _queuePosition;

    /// <summary>Total queue length while queued.</summary>
    [ObservableProperty]
    private int _queueTotal;

    /// <summary>"In queue: position 3 of 8" when queued, else empty.</summary>
    public string QueueStatusText =>
        IsQueued ? $"In queue: position {QueuePosition} of {QueueTotal}" : "";

    [ObservableProperty]
    private string _activeConversationTitle = "";

    [ObservableProperty]
    private string _composerText = "";

    [ObservableProperty]
    private string _streamingContent = "";

    [ObservableProperty]
    private bool _isStreaming;

    /// <summary>
    /// True while streaming when no reply content has arrived yet and the 3-second
    /// model-load window has elapsed — shows "Loading model into memory…" (mirrors Blazor).
    /// </summary>
    [ObservableProperty]
    private bool _isModelLoading;

    /// <summary>The model's live reasoning text while it is thinking (empty when none).</summary>
    [ObservableProperty]
    private string _streamingThinking = "";

    /// <summary>True once the model has emitted thinking text (shows the reasoning block).</summary>
    [ObservableProperty]
    private bool _hasThinking;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _ollamaHealthy = true;

    [ObservableProperty]
    private bool _showConversationList = true;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Id of the message most recently copied, for transient "Copied!" feedback.</summary>
    [ObservableProperty]
    private Guid? _copiedMessageId;

    partial void OnIsQueuedChanged(bool value) => OnPropertyChanged(nameof(QueueStatusText));

    partial void OnQueuePositionChanged(int value) => OnPropertyChanged(nameof(QueueStatusText));

    partial void OnQueueTotalChanged(int value) => OnPropertyChanged(nameof(QueueStatusText));

    partial void OnStreamingContentChanged(string value) =>
        OnPropertyChanged(nameof(StreamingDisplay));

    /// <summary>Streaming content with a trailing cursor, for the streaming bubble.</summary>
    public string StreamingDisplay =>
        string.IsNullOrEmpty(StreamingContent) ? "▍" : StreamingContent + "▍";

    private static AiConversationDto NormalizeTitle(AiConversationDto conversation) =>
        string.IsNullOrWhiteSpace(conversation.Title)
            ? conversation with { Title = "Untitled" }
            : conversation;

    // ── Loading ───────────────────────────────────────────────────────

    /// <summary>Loads models, conversations, and Ollama health on first appear.</summary>
    public async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var (serverUrl, token) = await GetCredentialsAsync();
            if (serverUrl is null || string.IsNullOrWhiteSpace(token))
            {
                ErrorMessage = "Not connected to a server.";
                IsLoading = false;
                return;
            }

            // Conversations are DB-backed and independent of the model provider, so load
            // them first — the page stays usable even when Ollama is unreachable.
            IReadOnlyList<AiConversationDto> conversations = [];
            try
            {
                conversations = await _ai.ListConversationsAsync(serverUrl, token);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load conversations: {ex.Message}";
            }

            // Ollama health + settings depend on the provider. When it is down the settings
            // call can throw (HTTP 500); surface the Ollama warning banner instead of a
            // hard failure so existing conversations remain visible.
            var healthy = false;
            var defaultModel = "";
            try
            {
                var healthTask = _ai.GetOllamaHealthAsync(serverUrl, token);
                var settingsTask = _ai.GetSettingsAsync(serverUrl, token);
                await Task.WhenAll(healthTask, settingsTask);
                healthy = await healthTask;
                var settings = await settingsTask;
                defaultModel = settings?.DefaultModel ?? "";
            }
            catch
            {
                healthy = false;
                ErrorMessage ??= "AI provider is unreachable.";
            }

            Dispatch(() =>
            {
                Conversations.Clear();
                foreach (var c in conversations)
                    Conversations.Add(NormalizeTitle(c));

                OllamaHealthy = healthy;
                DefaultModel = defaultModel;

                ShowConversationList = true;
                IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load AI data: {ex.Message}";
            IsLoading = false;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────

    /// <summary>Creates a new conversation and opens the chat view.</summary>
    [RelayCommand]
    private async Task NewConversationAsync()
    {
        ErrorMessage = null;
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || string.IsNullOrWhiteSpace(token))
        {
            ErrorMessage = "Not connected to a server.";
            return;
        }

        try
        {
            var created = await _ai.CreateConversationAsync(serverUrl, token, null);
            if (created is null)
            {
                ErrorMessage = "Failed to create conversation.";
                return;
            }

            Dispatch(() =>
            {
                Conversations.Insert(0, NormalizeTitle(created));
                ActiveConversationId = created.Id;
                ActiveConversationTitle = string.IsNullOrWhiteSpace(created.Title) ? "New Chat" : created.Title;
                ActiveConversationModel = created.Model;
                ActiveMessages.Clear();
                ComposerText = "";
                StreamingContent = "";
                ShowConversationList = false;
                ScrollRequested?.Invoke();
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create conversation: {ex.Message}";
        }
    }

    /// <summary>Opens an existing conversation and loads its messages.</summary>
    [RelayCommand]
    private async Task SelectConversationAsync(AiConversationDto conversation)
    {
        if (conversation is null)
            return;

        ErrorMessage = null;
        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || string.IsNullOrWhiteSpace(token))
        {
            ErrorMessage = "Not connected to a server.";
            return;
        }

        try
        {
            var detail = await _ai.GetConversationAsync(serverUrl, token, conversation.Id);
            Dispatch(() =>
            {
                ActiveConversationId = conversation.Id;
                ActiveConversationTitle = string.IsNullOrWhiteSpace(conversation.Title) ? "Untitled" : conversation.Title;
                ActiveMessages.Clear();
                if (detail?.Messages is not null)
                {
                    foreach (var m in detail.Messages)
                        ActiveMessages.Add(m);
                }
                ActiveConversationModel = string.IsNullOrEmpty(detail?.Model) ? DefaultModel : detail.Model;
                StreamingContent = "";
                ShowConversationList = false;
                ScrollRequested?.Invoke();
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load conversation: {ex.Message}";
        }
    }

    /// <summary>Sends the composer text and streams the assistant reply.</summary>
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var message = ComposerText?.Trim();
        if (string.IsNullOrEmpty(message) || IsStreaming || ActiveConversationId is null)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || string.IsNullOrWhiteSpace(token))
        {
            ErrorMessage = "Not connected to a server.";
            return;
        }

        var conversationId = ActiveConversationId.Value;
        ErrorMessage = null;

        // Cancel any in-flight stream and start fresh.
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = new CancellationTokenSource();
        var ct = _streamCts.Token;

        var userMessage = new AiMessageDto
        {
            Id = Guid.NewGuid(),
            Role = "user",
            Content = message,
            CreatedAt = DateTime.UtcNow
        };

        Dispatch(() =>
        {
            ActiveMessages.Add(userMessage);
            ComposerText = "";
            StreamingContent = "";
            StreamingThinking = "";
            HasThinking = false;
            IsStreaming = true;
            IsQueued = true;
            ScrollRequested?.Invoke();
        });

        var accumulated = new System.Text.StringBuilder();
        var thinking = new System.Text.StringBuilder();
        var startedGenerating = false;
        try
        {
            await foreach (var chunk in _ai.SendMessageStreamingAsync(serverUrl, token, conversationId, message, ct))
            {
                if (string.Equals(chunk.Status, "queued", StringComparison.OrdinalIgnoreCase))
                {
                    Dispatch(() =>
                    {
                        IsQueued = true;
                        if (chunk.Position is int p)
                            QueuePosition = p;
                        if (chunk.Total is int t)
                            QueueTotal = t;
                    });
                    continue;
                }

                if (!startedGenerating)
                {
                    // Request left the queue and is now generating — start the model-load timer.
                    startedGenerating = true;
                    Dispatch(() => IsQueued = false);
                    StartModelLoadTimer();
                }

                if (!string.IsNullOrEmpty(chunk.Thinking))
                {
                    // Surface the model's live reasoning instead of a stuck spinner.
                    thinking.Append(chunk.Thinking);
                    Dispatch(() =>
                    {
                        StreamingThinking = thinking.ToString();
                        HasThinking = true;
                        ScrollRequested?.Invoke();
                    });
                }

                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    accumulated.Append(chunk.Content);
                    // First real content means the model is loaded — hide the loading message.
                    if (IsModelLoading)
                        Dispatch(() => IsModelLoading = false);
                }

                Dispatch(() =>
                {
                    StreamingContent = accumulated.ToString();
                    ScrollRequested?.Invoke();
                });

                if (chunk.Done)
                    break;
            }

            var assistantText = accumulated.ToString();
            var assistantMessage = new AiMessageDto
            {
                Id = Guid.NewGuid(),
                Role = "assistant",
                Content = assistantText,
                CreatedAt = DateTime.UtcNow
            };

            Dispatch(() =>
            {
                if (!string.IsNullOrEmpty(assistantText))
                    ActiveMessages.Add(assistantMessage);
                StreamingContent = "";
                IsStreaming = false;
                IsQueued = false;
                ScrollRequested?.Invoke();
            });

            // Refresh the list so UpdatedAt/timestamps reflect the new message.
            await RefreshConversationsAsync(serverUrl, token);
        }
        catch (OperationCanceledException)
        {
            Dispatch(() =>
            {
                StreamingContent = "";
                IsStreaming = false;
                IsQueued = false;
            });
        }
        catch (Exception ex)
        {
            Dispatch(() =>
            {
                StreamingContent = "";
                IsStreaming = false;
                IsQueued = false;
                ErrorMessage = $"Failed to send message: {ex.Message}";
            });
        }
        finally
        {
            QueuePosition = 0;
            QueueTotal = 0;
            StreamingThinking = "";
            HasThinking = false;
            StopModelLoadTimer();
            _streamCts?.Dispose();
            _streamCts = null;
        }
    }

    /// <summary>
    /// Starts the 3-second window after which "Loading model into memory…" is shown if no
    /// reply content has arrived yet (mirrors the Blazor module's model-loading indicator).
    /// </summary>
    private void StartModelLoadTimer()
    {
        _modelLoadCts?.Cancel();
        _modelLoadCts?.Dispose();
        _modelLoadCts = new CancellationTokenSource();
        var ct = _modelLoadCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ModelLoadDelay, ct);
                if (!ct.IsCancellationRequested && IsStreaming && string.IsNullOrEmpty(StreamingContent) && string.IsNullOrEmpty(StreamingThinking))
                    Dispatch(() => IsModelLoading = true);
            }
            catch (OperationCanceledException)
            {
                // Stream produced content (or was cancelled) — nothing to do.
            }
        }, ct);
    }

    /// <summary>Cancels the model-load timer and clears the loading state.</summary>
    private void StopModelLoadTimer()
    {
        _modelLoadCts?.Cancel();
        _modelLoadCts?.Dispose();
        _modelLoadCts = null;
        IsModelLoading = false;
    }

    /// <summary>Deletes a conversation (from the swipe action).</summary>
    [RelayCommand]
    private async Task DeleteConversationAsync(AiConversationDto conversation)
    {
        if (conversation is null)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            var deleted = await _ai.DeleteConversationAsync(serverUrl, token, conversation.Id);
            if (!deleted)
                return;

            Dispatch(() =>
            {
                Conversations.Remove(conversation);
                if (ActiveConversationId == conversation.Id)
                {
                    ActiveConversationId = null;
                    ActiveMessages.Clear();
                    ShowConversationList = true;
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete conversation: {ex.Message}";
        }
    }

    /// <summary>Starts a rename for the given conversation; the page prompts for the new title.</summary>
    [RelayCommand]
    private void BeginRename(AiConversationDto conversation)
    {
        if (conversation is null)
            return;
        _renameTarget = conversation;
        RenameRequested?.Invoke(conversation);
    }

    /// <summary>Commits a rename using the title collected by the page prompt.</summary>
    public async Task CommitRenameAsync(string newTitle)
    {
        var target = _renameTarget;
        if (target is null)
            return;

        if (string.IsNullOrWhiteSpace(newTitle) || newTitle == target.Title)
            return;

        var (serverUrl, token) = await GetCredentialsAsync();
        if (serverUrl is null || string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            var renamed = await _ai.RenameConversationAsync(serverUrl, token, target.Id, newTitle);
            if (!renamed)
            {
                ErrorMessage = "Failed to rename conversation.";
                return;
            }

            Dispatch(() =>
            {
                var idx = Conversations.IndexOf(target);
                if (idx >= 0)
                {
                    Conversations[idx] = target with { Title = newTitle };
                    if (ActiveConversationId == target.Id)
                        ActiveConversationTitle = newTitle;
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to rename conversation: {ex.Message}";
        }
        finally
        {
            _renameTarget = null;
        }
    }

    /// <summary>Returns to the conversation list view.</summary>
    [RelayCommand]
    private void BackToList()
    {
        ShowConversationList = true;
    }

    /// <summary>Copies a message's content to the clipboard and shows brief "Copied!" feedback.</summary>
    [RelayCommand]
    private async Task CopyMessageAsync(AiMessageDto? message)
    {
        if (message is null || string.IsNullOrEmpty(message.Content))
            return;

        await _clipboard.SetTextAsync(message.Content);
        CopiedMessageId = message.Id;
        _ = ResetCopiedStateAsync(message.Id);
    }

    /// <summary>Clears the "Copied!" feedback after a short delay.</summary>
    private async Task ResetCopiedStateAsync(Guid id)
    {
        try
        {
            await Task.Delay(1500);
        }
        finally
        {
            if (CopiedMessageId == id)
                CopiedMessageId = null;
        }
    }

    private async Task RefreshConversationsAsync(string serverUrl, string token)
    {
        try
        {
            var conversations = await _ai.ListConversationsAsync(serverUrl, token);
            Dispatch(() =>
            {
                Conversations.Clear();
                foreach (var c in conversations)
                    Conversations.Add(NormalizeTitle(c));
            });
        }
        catch
        {
            // Non-fatal — keep the existing list.
        }
    }
}
