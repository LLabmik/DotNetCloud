using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Core.AI;
using DotNetCloud.Modules.AI.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.AI.Data.Services;

/// <summary>
/// HTTP client for communicating with an LLM provider (Ollama, OpenAI-compatible, etc.).
/// Wraps the REST API for chat completions, streaming, and model listing.
/// Reads the base URL dynamically from <see cref="IAiSettingsProvider"/> on each request
/// so that admin settings changes take effect without a restart.
/// </summary>
public sealed class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly IAiSettingsProvider _settingsProvider;
    private readonly ILogger<OllamaClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaClient"/> class.
    /// </summary>
    public OllamaClient(HttpClient httpClient, IAiSettingsProvider settingsProvider, ILogger<OllamaClient> logger)
    {
        _httpClient = httpClient;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = await GetBaseUrlAsync(cancellationToken);
            // Ollama exposes GET / (returns "Ollama is running"), not /health
            var response = await _httpClient.GetAsync(new Uri(baseUrl, "/"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama health check failed");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseUrl = await GetBaseUrlAsync(cancellationToken);
        var ollamaRequest = BuildChatRequest(request, stream: false);

        _logger.LogDebug("Sending chat request to Ollama: model={Model}, messages={Count}",
            request.Model, request.Messages.Count);

        var response = await _httpClient.PostAsJsonAsync(new Uri(baseUrl, "/api/chat"), ollamaRequest, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Ollama returned null response");

        return new LlmResponse
        {
            Model = ollamaResponse.Model ?? request.Model,
            Message = new LlmMessage("assistant", ollamaResponse.Message?.Content ?? string.Empty),
            Done = ollamaResponse.Done,
            TotalDurationNs = ollamaResponse.TotalDuration,
            PromptEvalCount = ollamaResponse.PromptEvalCount,
            EvalCount = ollamaResponse.EvalCount
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmResponseChunk> ChatStreamingAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseUrl = await GetBaseUrlAsync(cancellationToken);
        var ollamaRequest = BuildChatRequest(request, stream: true);

        _logger.LogDebug("Sending streaming chat request to Ollama: model={Model}, messages={Count}",
            request.Model, request.Messages.Count);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUrl, "/api/chat"))
        {
            Content = JsonContent.Create(ollamaRequest, options: JsonOptions)
        };

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            OllamaChatResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize Ollama streaming chunk: {Line}", line);
                continue;
            }

            if (chunk is null)
            {
                continue;
            }

            yield return new LlmResponseChunk
            {
                Model = chunk.Model ?? request.Model,
                Content = chunk.Message?.Content ?? string.Empty,
                Done = chunk.Done,
                TotalDurationNs = chunk.Done ? chunk.TotalDuration : null,
                EvalCount = chunk.Done ? chunk.EvalCount : null
            };
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = await GetBaseUrlAsync(cancellationToken);
        _logger.LogDebug("Listing models from Ollama at {BaseUrl}", baseUrl);

        var response = await _httpClient.GetAsync(new Uri(baseUrl, "/api/tags"), cancellationToken);
        response.EnsureSuccessStatusCode();

        var tagsResponse = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(JsonOptions, cancellationToken);

        if (tagsResponse?.Models is null)
        {
            return Array.Empty<LlmModelInfo>();
        }

        return tagsResponse.Models.Select(m => new LlmModelInfo
        {
            Id = m.Name ?? string.Empty,
            Name = m.Name ?? string.Empty,
            Provider = "ollama",
            SizeBytes = m.Size,
            ParameterSize = m.Details?.ParameterSize,
            ModifiedAt = m.ModifiedAt
        }).ToArray();
    }

    private static OllamaChatRequest BuildChatRequest(LlmRequest request, bool stream)
    {
        var messages = new List<OllamaChatMessage>();

        // Prepend system prompt if provided
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new OllamaChatMessage { Role = "system", Content = request.SystemPrompt });
        }

        messages.AddRange(request.Messages.Select(m => new OllamaChatMessage
        {
            Role = m.Role,
            Content = m.Content
        }));

        var chatRequest = new OllamaChatRequest
        {
            Model = request.Model,
            Messages = messages,
            Stream = stream
        };

        if (request.Temperature.HasValue || request.MaxTokens.HasValue)
        {
            chatRequest.Options = new OllamaOptions
            {
                Temperature = request.Temperature,
                NumPredict = request.MaxTokens
            };
        }

        return chatRequest;
    }

    private async Task<Uri> GetBaseUrlAsync(CancellationToken cancellationToken)
    {
        var url = await _settingsProvider.GetApiBaseUrlAsync(cancellationToken);
        return new Uri(url.TrimEnd('/') + "/");
    }

    // --- Ollama API DTOs (internal) ---

    private sealed class OllamaChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OllamaChatMessage> Messages { get; set; } = [];
        public bool Stream { get; set; }
        public OllamaOptions? Options { get; set; }
    }

    private sealed class OllamaChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaOptions
    {
        public double? Temperature { get; set; }
        public int? NumPredict { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        public string? Model { get; set; }
        public OllamaChatMessage? Message { get; set; }
        public bool Done { get; set; }
        public long? TotalDuration { get; set; }
        public long? LoadDuration { get; set; }
        public int? PromptEvalCount { get; set; }
        public int? EvalCount { get; set; }
    }

    private sealed class OllamaTagsResponse
    {
        public List<OllamaModelInfo>? Models { get; set; }
    }

    private sealed class OllamaModelInfo
    {
        public string? Name { get; set; }
        public long? Size { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public OllamaModelDetails? Details { get; set; }
    }

    private sealed class OllamaModelDetails
    {
        public string? ParameterSize { get; set; }
        public string? QuantizationLevel { get; set; }
        public string? Format { get; set; }
        public string? Family { get; set; }
    }
}
