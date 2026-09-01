using System.Runtime.CompilerServices;
using DotNetCloud.Core.AI;
using DotNetCloud.Modules.AI.Host.Protos;
using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the AI gRPC client used by the Core Server.
/// </summary>
public sealed class AiGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "AiGrpc";

    /// <summary>The gRPC address of the AI module.</summary>
    public string AiModuleAddress { get; set; } = "http://localhost:5015";

    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Deadline for long-lived streaming calls (queue wait + generation). Default: 30 minutes.</summary>
    public TimeSpan StreamTimeout { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// gRPC implementation of <see cref="IAiApiClient"/>.
/// Calls the AI module's gRPC service for LLM chat operations including streaming.
/// </summary>
public sealed class AiGrpcApiClient : IAiApiClient, IDisposable
{
    private readonly AiGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<AiGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<AiService.AiServiceClient> _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="AiGrpcApiClient"/> class.</summary>
    public AiGrpcApiClient(
        IOptions<AiGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        ILogger<AiGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<AiService.AiServiceClient>(
            () => new AiService.AiServiceClient(_channel.Value));
    }

    /// <inheritdoc />
    public async Task<ConversationDto?> CreateConversationAsync(Guid userId, string? title, string? systemPrompt, CancellationToken ct = default)
        => await SafeCall(async () =>
        {
            var request = new CreateConversationRequest
            {
                UserId = userId.ToString(),
                Title = title ?? "",
                SystemPrompt = systemPrompt ?? ""
            };
            var resp = await _client.Value.CreateConversationAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return resp.Success ? ToConversation(resp.Conversation) : null;
        }, "CreateConversation");

    /// <inheritdoc />
    public async Task<ConversationDetailDto?> GetConversationAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
        => await SafeCall(async () =>
        {
            var request = new GetConversationRequest
            {
                ConversationId = conversationId.ToString(),
                UserId = userId.ToString()
            };
            var resp = await _client.Value.GetConversationAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return resp.Success ? ToConversationDetail(resp.Conversation) : null;
        }, "GetConversation");

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConversationSummaryDto>> ListConversationsAsync(Guid userId, CancellationToken ct = default)
        => (await SafeCall<IReadOnlyList<ConversationSummaryDto>>(async () =>
        {
            var request = new ListConversationsRequest { UserId = userId.ToString() };
            var resp = await _client.Value.ListConversationsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            if (!resp.Success)
                return [];
            return resp.Conversations.Select(c => new ConversationSummaryDto
            {
                Id = Guid.Parse(c.Id),
                Title = c.Title,
                Model = c.Model,
                CreatedAt = DateTime.Parse(c.CreatedAt),
                UpdatedAt = DateTime.Parse(c.UpdatedAt)
            }).ToList();
        }, "ListConversations", []))!;

    /// <inheritdoc />
    public async Task<bool> DeleteConversationAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
        => (await SafeCall(async () =>
        {
            var request = new DeleteConversationRequest
            {
                ConversationId = conversationId.ToString(),
                UserId = userId.ToString()
            };
            var resp = await _client.Value.DeleteConversationAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return resp.Success && resp.Deleted;
        }, "DeleteConversation", false))!;

    /// <inheritdoc />
    public async Task<bool> RenameConversationAsync(Guid userId, Guid conversationId, string newTitle, CancellationToken ct = default)
        => (await SafeCall(async () =>
        {
            var request = new RenameConversationRequest
            {
                ConversationId = conversationId.ToString(),
                UserId = userId.ToString(),
                NewTitle = newTitle
            };
            var resp = await _client.Value.RenameConversationAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return resp.Success;
        }, "RenameConversation", false))!;

    /// <inheritdoc />
    public async Task<ChatResponseDto?> SendMessageAsync(Guid userId, Guid conversationId, string message, CancellationToken ct = default)
        => await SafeCall(async () =>
        {
            var request = new SendMessageRequest
            {
                ConversationId = conversationId.ToString(),
                UserId = userId.ToString(),
                Message = message
            };
            var resp = await _client.Value.SendMessageAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return resp.Success
                ? new ChatResponseDto
                {
                    Model = resp.Model,
                    Content = resp.Content,
                    Done = resp.Done,
                    PromptEvalCount = resp.PromptEvalCount,
                    EvalCount = resp.EvalCount
                }
                : null;
        }, "SendMessage");

    /// <inheritdoc />
    public async IAsyncEnumerable<MessageChunkDto> SendMessageStreamingAsync(
        Guid userId, Guid conversationId, string message, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new SendMessageRequest
        {
            ConversationId = conversationId.ToString(),
            UserId = userId.ToString(),
            Message = message
        };

        // Use the long streaming deadline — queue waits + generation can exceed the
        // normal 30s call timeout.
        using var call = _client.Value.SendMessageStream(request, new CallOptions(
            deadline: DateTime.UtcNow.Add(_options.StreamTimeout),
            cancellationToken: ct));
        await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
        {
            yield return new MessageChunkDto
            {
                Content = chunk.Content,
                Done = chunk.Done,
                EvalCount = chunk.EvalCount,
                Thinking = chunk.Thinking,
                Status = (LlmStreamStatus)(int)chunk.Status,
                QueuedPosition = chunk.QueuedPosition > 0 ? chunk.QueuedPosition : null,
                QueueTotal = chunk.QueueTotal > 0 ? chunk.QueueTotal : null
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsOllamaHealthyAsync(CancellationToken ct = default)
        => (await SafeCall(async () =>
        {
            var resp = await _client.Value.GetOllamaHealthAsync(
                new GetOllamaHealthRequest { UserId = Guid.Empty.ToString() }, DeadlineHeaders(ct)).ResponseAsync;
            return resp.Healthy;
        }, "GetOllamaHealth", false))!;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModelInfoDto>> ListModelsAsync(CancellationToken ct = default)
        => (await SafeCall<IReadOnlyList<ModelInfoDto>>(async () =>
        {
            var request = new ListModelsRequest { UserId = Guid.Empty.ToString() };
            var resp = await _client.Value.ListModelsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            if (!resp.Success)
                return [];
            return resp.Models.Select(m => new ModelInfoDto { Name = m.Name, Provider = m.Provider }).ToList();
        }, "ListModels", []))!;

    /// <inheritdoc />
    public async Task<SettingsDto?> GetSettingsAsync(CancellationToken ct = default)
        => await SafeCall(async () =>
        {
            var request = new GetSettingsRequest { UserId = Guid.Empty.ToString() };
            var resp = await _client.Value.GetSettingsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return resp.Success
                ? new SettingsDto
                {
                    Provider = resp.Provider,
                    ApiBaseUrl = resp.ApiBaseUrl,
                    DefaultModel = resp.DefaultModel,
                    MaxTokens = resp.MaxTokens,
                    RequestTimeoutSeconds = resp.RequestTimeoutSeconds
                }
                : null;
        }, "GetSettings");

    /// <inheritdoc />
    public async Task<SettingsDto?> UpdateSettingsAsync(SettingsDto dto, CancellationToken ct = default)
        => await SafeCall(async () =>
        {
            var request = new UpdateSettingsRequest
            {
                UserId = Guid.Empty.ToString(),
                Provider = dto.Provider,
                ApiBaseUrl = dto.ApiBaseUrl,
                DefaultModel = dto.DefaultModel,
                MaxTokens = dto.MaxTokens,
                RequestTimeoutSeconds = dto.RequestTimeoutSeconds
            };
            var resp = await _client.Value.UpdateSettingsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return resp.Success
                ? new SettingsDto
                {
                    Provider = resp.Provider,
                    ApiBaseUrl = resp.ApiBaseUrl,
                    DefaultModel = resp.DefaultModel,
                    MaxTokens = resp.MaxTokens,
                    RequestTimeoutSeconds = resp.RequestTimeoutSeconds
                }
                : null;
        }, "UpdateSettings");

    private static ConversationDto ToConversation(ConversationMessage c)
        => new()
        {
            Id = Guid.Parse(c.Id),
            Title = c.Title,
            Model = c.Model,
            SystemPrompt = string.IsNullOrEmpty(c.SystemPrompt) ? null : c.SystemPrompt,
            CreatedAt = DateTime.Parse(c.CreatedAt),
            UpdatedAt = DateTime.Parse(c.UpdatedAt)
        };

    private static ConversationDetailDto ToConversationDetail(ConversationMessage c)
        => new()
        {
            Id = Guid.Parse(c.Id),
            Title = c.Title,
            Model = c.Model,
            SystemPrompt = string.IsNullOrEmpty(c.SystemPrompt) ? null : c.SystemPrompt,
            CreatedAt = DateTime.Parse(c.CreatedAt),
            UpdatedAt = DateTime.Parse(c.UpdatedAt),
            Messages = c.Messages.Select(m => new MessageDto
            {
                Id = Guid.Parse(m.Id),
                Role = m.Role,
                Content = m.Content,
                CreatedAt = DateTime.Parse(m.CreatedAt)
            }).ToList()
        };

    private async Task<T?> SafeCall<T>(Func<Task<T?>> call, string operation, T? fallback = default)
    {
        try
        { return await call(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
        { _logger.LogWarning(ex, "AI gRPC {Operation} unavailable", operation); return fallback; }
        catch (Exception ex) { _logger.LogError(ex, "AI gRPC {Operation} error", operation); return fallback; }
    }

    private CallOptions DeadlineHeaders(CancellationToken ct)
        => new(deadline: DateTime.UtcNow.Add(_options.Timeout), cancellationToken: ct);

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.ai");
        _logger.LogInformation("AiGrpcApiClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_channel.IsValueCreated)
            {
                try
                { _channel.Value.Dispose(); }
                catch { /* best effort */ }
            }
        }
    }
}
