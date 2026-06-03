using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.AI.Host.Protos;
using DotNetCloud.Modules.AI.Services;
using Grpc.Core;

namespace DotNetCloud.Modules.AI.Host.Services;

/// <summary>
/// gRPC service for the AI module — provides LLM-powered chat, conversation management,
/// model listing, and settings via gRPC.
/// </summary>
public sealed class AiGrpcService : AiService.AiServiceBase
{
    private readonly IAiChatService _chatService;
    private readonly IAiSettingsProvider _settingsProvider;
    private readonly ILogger<AiGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiGrpcService"/> class.
    /// </summary>
    public AiGrpcService(
        IAiChatService chatService,
        IAiSettingsProvider settingsProvider,
        ILogger<AiGrpcService> logger)
    {
        _chatService = chatService;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ConversationResponse> CreateConversation(
        CreateConversationRequest request, ServerCallContext context)
    {
        try
        {
            var caller = CallerContext.CreateSystemContext();
            var defaultModel = await _settingsProvider.GetDefaultModelAsync(context.CancellationToken);
            var model = string.IsNullOrWhiteSpace(request.Model) ? defaultModel : request.Model;
            var conversation = await _chatService.CreateConversationAsync(
                caller,
                string.IsNullOrWhiteSpace(request.Title) ? null : request.Title,
                model,
                string.IsNullOrWhiteSpace(request.SystemPrompt) ? null : request.SystemPrompt,
                context.CancellationToken);

            return new ConversationResponse
            {
                Success = true,
                Conversation = MapConversation(conversation)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateConversation failed");
            return new ConversationResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ConversationResponse> GetConversation(
        GetConversationRequest request, ServerCallContext context)
    {
        try
        {
            var conversation = await _chatService.GetConversationAsync(
                CallerContext.CreateSystemContext(),
                Guid.Parse(request.ConversationId),
                context.CancellationToken);

            if (conversation is null)
                return new ConversationResponse { Success = false, ErrorMessage = "Not found" };

            return new ConversationResponse
            {
                Success = true,
                Conversation = MapConversationWithMessages(conversation)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetConversation failed");
            return new ConversationResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListConversationsResponse> ListConversations(
        ListConversationsRequest request, ServerCallContext context)
    {
        try
        {
            var list = await _chatService.ListConversationsAsync(
                CallerContext.CreateSystemContext(),
                context.CancellationToken);

            var response = new ListConversationsResponse { Success = true };
            foreach (var c in list)
                response.Conversations.Add(MapSummary(c));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListConversations failed");
            return new ListConversationsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<DeleteConversationResponse> DeleteConversation(
        DeleteConversationRequest request, ServerCallContext context)
    {
        try
        {
            var deleted = await _chatService.DeleteConversationAsync(
                CallerContext.CreateSystemContext(),
                Guid.Parse(request.ConversationId),
                context.CancellationToken);

            return new DeleteConversationResponse { Success = true, Deleted = deleted };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteConversation failed");
            return new DeleteConversationResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ConversationResponse> RenameConversation(
        RenameConversationRequest request, ServerCallContext context)
    {
        try
        {
            var ok = await _chatService.RenameConversationAsync(
                CallerContext.CreateSystemContext(),
                Guid.Parse(request.ConversationId),
                request.NewTitle,
                context.CancellationToken);

            return new ConversationResponse { Success = ok };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RenameConversation failed");
            return new ConversationResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> SendMessage(
        SendMessageRequest request, ServerCallContext context)
    {
        try
        {
            var response = await _chatService.SendMessageAsync(
                CallerContext.CreateSystemContext(),
                Guid.Parse(request.ConversationId),
                request.Message,
                context.CancellationToken);

            return new ChatResponse
            {
                Success = true,
                Model = response.Model,
                Content = response.Message.Content,
                Done = response.Done,
                PromptEvalCount = response.PromptEvalCount,
                EvalCount = response.EvalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendMessage failed");
            return new ChatResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task SendMessageStream(
        SendMessageRequest request,
        IServerStreamWriter<MessageChunk> stream,
        ServerCallContext context)
    {
        await foreach (var chunk in _chatService.SendMessageStreamingAsync(
            CallerContext.CreateSystemContext(),
            Guid.Parse(request.ConversationId),
            request.Message,
            context.CancellationToken))
        {
            await stream.WriteAsync(new MessageChunk
            {
                Content = chunk.Content,
                Done = chunk.Done,
                EvalCount = chunk.EvalCount
            });
        }
    }

    /// <inheritdoc />
    public override async Task<ListModelsResponse> ListModels(
        ListModelsRequest request, ServerCallContext context)
    {
        try
        {
            var models = await _chatService.ListModelsAsync(
                CallerContext.CreateSystemContext(),
                context.CancellationToken);

            var response = new ListModelsResponse { Success = true };
            foreach (var m in models)
                response.Models.Add(new ModelInfoMessage { Name = m.Name, Provider = m.Provider });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListModels failed");
            return new ListModelsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<SettingsResponse> GetSettings(
        GetSettingsRequest request, ServerCallContext context)
    {
        try
        {
            return new SettingsResponse
            {
                Success = true,
                Provider = await _settingsProvider.GetProviderAsync(context.CancellationToken),
                ApiBaseUrl = await _settingsProvider.GetApiBaseUrlAsync(context.CancellationToken),
                DefaultModel = await _settingsProvider.GetDefaultModelAsync(context.CancellationToken),
                MaxTokens = await _settingsProvider.GetMaxTokensAsync(context.CancellationToken),
                RequestTimeoutSeconds = await _settingsProvider.GetRequestTimeoutSecondsAsync(context.CancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSettings failed");
            return new SettingsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override Task<SettingsResponse> UpdateSettings(
        UpdateSettingsRequest request, ServerCallContext context)
    {
        return Task.FromResult(new SettingsResponse
        {
            Success = true,
            Provider = request.Provider,
            ApiBaseUrl = request.ApiBaseUrl,
            DefaultModel = request.DefaultModel,
            MaxTokens = request.MaxTokens,
            RequestTimeoutSeconds = request.RequestTimeoutSeconds
        });
    }

    private static ConversationMessage MapConversation(Models.Conversation c)
        => new()
        {
            Id = c.Id.ToString(),
            Title = c.Title,
            Model = c.Model,
            SystemPrompt = c.SystemPrompt ?? "",
            CreatedAt = c.CreatedAt.ToString("O"),
            UpdatedAt = c.UpdatedAt.ToString("O")
        };

    private static ConversationMessage MapConversationWithMessages(Models.Conversation c)
    {
        var msg = MapConversation(c);
        msg.Messages.AddRange(c.Messages.Select(x => new MessageDtoMessage
        {
            Id = x.Id.ToString(),
            Role = x.Role,
            Content = x.Content,
            CreatedAt = x.CreatedAt.ToString("O")
        }));
        return msg;
    }

    private static ConversationSummaryMessage MapSummary(Models.Conversation c)
        => new()
        {
            Id = c.Id.ToString(),
            Title = c.Title,
            Model = c.Model,
            CreatedAt = c.CreatedAt.ToString("O"),
            UpdatedAt = c.UpdatedAt.ToString("O")
        };
}
