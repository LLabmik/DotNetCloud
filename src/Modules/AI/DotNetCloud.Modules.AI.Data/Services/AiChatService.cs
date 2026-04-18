using System.Runtime.CompilerServices;
using DotNetCloud.Core.AI;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.AI.Models;
using DotNetCloud.Modules.AI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.AI.Data.Services;

/// <summary>
/// Service implementation for AI chat operations.
/// Manages conversations, persists history, and routes requests to the Ollama provider.
/// </summary>
public sealed class AiChatService : IAiChatService
{
    private readonly AiDbContext _db;
    private readonly IOllamaClient _ollamaClient;
    private readonly ILogger<AiChatService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiChatService"/> class.
    /// </summary>
    public AiChatService(AiDbContext db, IOllamaClient ollamaClient, ILogger<AiChatService> logger)
    {
        _db = db;
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Conversation> CreateConversationAsync(
        CallerContext caller,
        string? title,
        string model,
        string? systemPrompt,
        CancellationToken cancellationToken = default)
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            OwnerId = caller.UserId,
            Title = title ?? "New Conversation",
            Model = model,
            SystemPrompt = systemPrompt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(cancellationToken);

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
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = "user",
            Content = userMessage,
            CreatedAt = DateTime.UtcNow
        };
        _db.ConversationMessages.Add(userMsg);

        // Build LLM request from conversation history
        var llmRequest = BuildLlmRequest(conversation);

        // Call Ollama
        var response = await _ollamaClient.ChatAsync(llmRequest, cancellationToken);

        // Persist the assistant response
        var assistantMsg = new ConversationMessage
        {
            Id = Guid.NewGuid(),
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
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = "user",
            Content = userMessage,
            CreatedAt = DateTime.UtcNow
        };
        _db.ConversationMessages.Add(userMsg);
        await _db.SaveChangesAsync(cancellationToken);

        // Build LLM request from conversation history
        var llmRequest = BuildLlmRequest(conversation);

        // Stream from Ollama and accumulate the full response
        var fullContent = new System.Text.StringBuilder();
        int? evalCount = null;

        await foreach (var chunk in _ollamaClient.ChatStreamingAsync(llmRequest, cancellationToken))
        {
            fullContent.Append(chunk.Content);

            if (chunk.Done)
            {
                evalCount = chunk.EvalCount;
            }

            yield return chunk;
        }

        // Persist the complete assistant response
        var assistantMsg = new ConversationMessage
        {
            Id = Guid.NewGuid(),
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
