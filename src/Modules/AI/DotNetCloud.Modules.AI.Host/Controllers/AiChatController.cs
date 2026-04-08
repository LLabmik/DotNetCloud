using System.Text.Json.Serialization;
using DotNetCloud.Core.AI;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.AI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.AI.Host.Controllers;

/// <summary>
/// REST API controller for AI chat operations.
/// </summary>
[ApiController]
[Route("api/ai")]
public sealed class AiChatController : ControllerBase
{
    private readonly IAiChatService _chatService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiChatController"/> class.
    /// </summary>
    public AiChatController(IAiChatService chatService, IConfiguration configuration)
    {
        _chatService = chatService;
        _configuration = configuration;
    }

    /// <summary>Creates a new conversation.</summary>
    [HttpPost("conversations")]
    public async Task<IActionResult> CreateConversation(
        [FromBody] CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        var caller = GetCallerContext();
        var defaultModel = _configuration.GetValue<string>("AI:Ollama:DefaultModel") ?? "gpt-oss:20b";
        var model = string.IsNullOrWhiteSpace(request.Model) ? defaultModel : request.Model;

        var conversation = await _chatService.CreateConversationAsync(
            caller, request.Title, model, request.SystemPrompt, cancellationToken);

        return Ok(new ConversationDto
        {
            Id = conversation.Id,
            Title = conversation.Title,
            Model = conversation.Model,
            SystemPrompt = conversation.SystemPrompt,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt
        });
    }

    /// <summary>Gets a conversation by ID with its messages.</summary>
    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<IActionResult> GetConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        var caller = GetCallerContext();
        var conversation = await _chatService.GetConversationAsync(caller, conversationId, cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        return Ok(new ConversationDto
        {
            Id = conversation.Id,
            Title = conversation.Title,
            Model = conversation.Model,
            SystemPrompt = conversation.SystemPrompt,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            Messages = conversation.Messages.Select(m => new MessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            }).ToList()
        });
    }

    /// <summary>Lists all conversations for the current user.</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> ListConversations(CancellationToken cancellationToken)
    {
        var caller = GetCallerContext();
        var conversations = await _chatService.ListConversationsAsync(caller, cancellationToken);

        return Ok(conversations.Select(c => new ConversationDto
        {
            Id = c.Id,
            Title = c.Title,
            Model = c.Model,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }));
    }

    /// <summary>Deletes a conversation (soft-delete).</summary>
    [HttpDelete("conversations/{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        var caller = GetCallerContext();
        var deleted = await _chatService.DeleteConversationAsync(caller, conversationId, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Sends a message and gets the full response.</summary>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var caller = GetCallerContext();

        try
        {
            var response = await _chatService.SendMessageAsync(caller, conversationId, request.Message, cancellationToken);

            return Ok(new ChatResponseDto
            {
                Model = response.Model,
                Content = response.Message.Content,
                Done = response.Done,
                PromptEvalCount = response.PromptEvalCount,
                EvalCount = response.EvalCount
            });
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>Sends a message and streams the response via Server-Sent Events.</summary>
    [HttpPost("conversations/{conversationId:guid}/messages/stream")]
    public async Task StreamMessage(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var caller = GetCallerContext();

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            await foreach (var chunk in _chatService.SendMessageStreamingAsync(
                caller, conversationId, request.Message, cancellationToken))
            {
                var data = System.Text.Json.JsonSerializer.Serialize(new
                {
                    content = chunk.Content,
                    done = chunk.Done,
                    evalCount = chunk.EvalCount
                });

                await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            Response.StatusCode = 404;
        }
    }

    /// <summary>Lists available models.</summary>
    [HttpGet("models")]
    public async Task<IActionResult> ListModels(CancellationToken cancellationToken)
    {
        var caller = GetCallerContext();
        var models = await _chatService.ListModelsAsync(caller, cancellationToken);
        return Ok(models);
    }

    /// <summary>Checks Ollama health.</summary>
    [HttpGet("health/ollama")]
    public async Task<IActionResult> OllamaHealth(
        [FromServices] IOllamaClient ollamaClient,
        CancellationToken cancellationToken)
    {
        var healthy = await ollamaClient.IsHealthyAsync(cancellationToken);
        return healthy
            ? Ok(new { status = "healthy" })
            : StatusCode(503, new { status = "unhealthy" });
    }

    private CallerContext GetCallerContext()
    {
        // Extract from authenticated user claims if available; fallback to dev context
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
            return new CallerContext(userId, roles, CallerType.User);
        }

        // Development fallback — creates a system context
        return CallerContext.CreateSystemContext();
    }
}

/// <summary>Request to create a new conversation.</summary>
public sealed class CreateConversationRequest
{
    /// <summary>Optional title for the conversation.</summary>
    public string? Title { get; set; }

    /// <summary>Model to use (defaults to configured default).</summary>
    public string? Model { get; set; }

    /// <summary>Optional system prompt.</summary>
    public string? SystemPrompt { get; set; }
}

/// <summary>Request to send a message.</summary>
public sealed class SendMessageRequest
{
    /// <summary>The user message text.</summary>
    public required string Message { get; set; }
}

/// <summary>DTO for conversation data.</summary>
public sealed class ConversationDto
{
    /// <summary>Conversation ID.</summary>
    public Guid Id { get; set; }

    /// <summary>Conversation title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Model in use.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>System prompt, if any.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>When created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When last updated.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Messages in the conversation (when retrieved with detail).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<MessageDto>? Messages { get; set; }
}

/// <summary>DTO for a single message.</summary>
public sealed class MessageDto
{
    /// <summary>Message ID.</summary>
    public Guid Id { get; set; }

    /// <summary>Role: "user", "assistant", "system".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Message content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>When created.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>DTO for a non-streaming chat response.</summary>
public sealed class ChatResponseDto
{
    /// <summary>Model that generated the response.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>The assistant's response content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Whether generation is complete.</summary>
    public bool Done { get; set; }

    /// <summary>Prompt tokens evaluated.</summary>
    public int? PromptEvalCount { get; set; }

    /// <summary>Response tokens generated.</summary>
    public int? EvalCount { get; set; }
}
