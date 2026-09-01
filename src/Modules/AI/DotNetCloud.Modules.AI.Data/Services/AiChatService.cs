using System.Runtime.CompilerServices;
using DotNetCloud.Core.AI;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.AI.Models;
using DotNetCloud.Modules.AI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IAuditLogger = DotNetCloud.Core.Capabilities.IAuditLogger;
using AuditEntry = DotNetCloud.Core.Capabilities.AuditEntry;
using AuditAction = DotNetCloud.Core.Capabilities.AuditAction;

namespace DotNetCloud.Modules.AI.Data.Services;

/// <summary>
/// Service implementation for AI chat operations.
/// Manages conversations, persists history, and routes requests to the Ollama provider.
/// </summary>
public sealed class AiChatService : IAiChatService
{
    private readonly AiDbContext _db;
    private readonly IOllamaClient _ollamaClient;
    private readonly IAiCompletionQueue _queue;
    private readonly IAiSettingsProvider _settingsProvider;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<AiChatService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiChatService"/> class.
    /// </summary>
    public AiChatService(
        AiDbContext db,
        IOllamaClient ollamaClient,
        IAiCompletionQueue queue,
        IAiSettingsProvider settingsProvider,
        IAuditLogger auditLogger,
        ILogger<AiChatService> logger)
    {
        _db = db;
        _ollamaClient = ollamaClient;
        _queue = queue;
        _settingsProvider = settingsProvider;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Conversation> CreateConversationAsync(
        CallerContext caller,
        string? title,
        string? systemPrompt,
        CancellationToken cancellationToken = default)
    {
        // Users cannot choose a model — always use the admin-configured default.
        var model = await _settingsProvider.GetDefaultModelAsync(cancellationToken);

        var conversation = new Conversation
        {
            Id = Guid.CreateVersion7(),
            OwnerId = caller.UserId,
            Title = title ?? "New Conversation",
            Model = model,
            SystemPrompt = systemPrompt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = caller,
            ModuleId = "dotnetcloud.ai",
            Action = AuditAction.Create,
            EntityType = "Conversation",
            EntityId = conversation.Id,
            Description = "create-conversation",
        }, cancellationToken);

        _logger.LogInformation("Created conversation {ConversationId} for user {UserId} with model {Model}",
            conversation.Id, caller.UserId, model);

        return conversation;
    }

    /// <inheritdoc />
    public async Task<Conversation?> GetConversationAsync(
        CallerContext caller,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.OwnerId == caller.UserId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Conversation>> ListConversationsAsync(
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        return await _db.Conversations
            .Where(c => c.OwnerId == caller.UserId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteConversationAsync(
        CallerContext caller,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.OwnerId == caller.UserId, cancellationToken);

        if (conversation is null)
        {
            return false;
        }

        conversation.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Soft-deleted conversation {ConversationId} for user {UserId}",
            conversationId, caller.UserId);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RenameConversationAsync(
        CallerContext caller,
        Guid conversationId,
        string newTitle,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.OwnerId == caller.UserId, cancellationToken);

        if (conversation is null)
        {
            return false;
        }

        conversation.Title = newTitle.Trim();
        conversation.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Renamed conversation {ConversationId} for user {UserId}",
            conversationId, caller.UserId);

        return true;
    }

    /// <inheritdoc />
    public async Task<LlmResponse> SendMessageAsync(
        CallerContext caller,
        Guid conversationId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var conversation = await GetConversationAsync(caller, conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation {conversationId} not found or not owned by caller.");

        // Persist the user message
        var userMsg = new ConversationMessage
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversationId,
            Role = "user",
            Content = userMessage,
            CreatedAt = DateTime.UtcNow
        };
        _db.ConversationMessages.Add(userMsg);

        // Build LLM request from conversation history
        var llmRequest = BuildLlmRequest(conversation);

        // Call Ollama through the FIFO queue so only one inference runs at a time.
        var response = await _queue.EnqueueTaskAsync(
            ct => _ollamaClient.ChatAsync(llmRequest, ct),
            cancellationToken);

        // Persist the assistant response
        var assistantMsg = new ConversationMessage
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversationId,
            Role = "assistant",
            Content = response.Message.Content,
            TokenCount = response.EvalCount,
            CreatedAt = DateTime.UtcNow
        };
        _db.ConversationMessages.Add(assistantMsg);

        // Auto-title from first user message if still default
        UpdateTitleFromFirstMessage(conversation, userMessage);

        // Update conversation timestamp
        conversation.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Chat completion for conversation {ConversationId}: {PromptTokens} prompt tokens, {EvalTokens} eval tokens",
            conversationId, response.PromptEvalCount, response.EvalCount);

        return response;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmResponseChunk> SendMessageStreamingAsync(
        CallerContext caller,
        Guid conversationId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversation = await GetConversationAsync(caller, conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation {conversationId} not found or not owned by caller.");

        // Persist the user message
        var userMsg = new ConversationMessage
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversationId,
            Role = "user",
            Content = userMessage,
            CreatedAt = DateTime.UtcNow
        };
        _db.ConversationMessages.Add(userMsg);
        await _db.SaveChangesAsync(cancellationToken);

        // Build LLM request from conversation history
        var llmRequest = BuildLlmRequest(conversation);

        // Enqueue the Ollama streaming call through the FIFO queue. The queue serializes
        // inference and emits a Generating marker once this request reaches the front.
        var entry = _queue.EnqueueStreaming(
            ct => _ollamaClient.ChatStreamingAsync(llmRequest, ct),
            cancellationToken);

        var reader = entry.Reader;
        var fullContent = new System.Text.StringBuilder();
        int? evalCount = null;
        var startedGenerating = false;

        // Interleave live queue-position updates while waiting for the turn, then
        // forward the streamed content once generation begins.
        while (!cancellationToken.IsCancellationRequested)
        {
            var readTask = reader.WaitToReadAsync(cancellationToken).AsTask();
            var delayTask = Task.Delay(500, cancellationToken);
            var completed = await Task.WhenAny(readTask, delayTask);

            if (completed == readTask)
            {
                // Channel completed with no more data — done.
                if (!await readTask)
                {
                    // Surface any error that terminated generation instead of silently
                    // ending the stream (the queue worker completes the channel with it).
                    await reader.Completion;
                    break;
                }

                while (reader.TryRead(out var chunk))
                {
                    if (chunk.Status == LlmStreamStatus.Queued)
                    {
                        yield return chunk;
                        continue;
                    }

                    startedGenerating = true;

                    fullContent.Append(chunk.Content);
                    if (chunk.Done)
                    {
                        evalCount = chunk.EvalCount;
                    }

                    yield return chunk;
                }
            }
            else if (!startedGenerating)
            {
                // Still waiting for the queue turn — report the live position.
                yield return new LlmResponseChunk
                {
                    Model = llmRequest.Model,
                    Content = string.Empty,
                    Status = LlmStreamStatus.Queued,
                    Done = false,
                    QueuedPosition = entry.Position,
                    QueueTotal = entry.Total
                };
            }
            // else: generating is in flight but the first token hasn't arrived yet —
            // keep waiting silently so the client stays in its "thinking" state.
        }

        // Persist the complete assistant response
        var assistantMsg = new ConversationMessage
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversationId,
            Role = "assistant",
            Content = fullContent.ToString(),
            TokenCount = evalCount,
            CreatedAt = DateTime.UtcNow
        };
        _db.ConversationMessages.Add(assistantMsg);

        // Auto-title from first user message if still default
        UpdateTitleFromFirstMessage(conversation, userMessage);

        conversation.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        return await _ollamaClient.ListModelsAsync(cancellationToken);
    }

    private static LlmRequest BuildLlmRequest(Conversation conversation)
    {
        var messages = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new LlmMessage(m.Role, m.Content))
            .ToList();

        return new LlmRequest
        {
            Model = conversation.Model,
            Messages = messages,
            SystemPrompt = conversation.SystemPrompt
        };
    }

    /// <summary>
    /// Sets the conversation title from the first user message if the title is still a default placeholder.
    /// </summary>
    private static void UpdateTitleFromFirstMessage(Conversation conversation, string userMessage)
    {
        if (string.IsNullOrWhiteSpace(conversation.Title)
            || conversation.Title is "New Chat" or "New Conversation"
            || conversation.Title.StartsWith("New ", StringComparison.OrdinalIgnoreCase))
        {
            // Only auto-title on the first user message (no prior user messages persisted yet,
            // since the current one was just added)
            var userMessageCount = conversation.Messages.Count(m => m.Role == "user");
            if (userMessageCount <= 1)
            {
                conversation.Title = userMessage.Length > 60
                    ? userMessage[..60].TrimEnd() + "\u2026"
                    : userMessage;
            }
        }
    }
}
