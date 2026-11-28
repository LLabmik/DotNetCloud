using System.Net.Http.Json;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.AI.Web.Services;

/// <summary>
/// The AI module UI's client for its own host's REST API.
/// </summary>
/// <remarks>
/// URLs are relative: the core proxies <c>/apps/dotnetcloud.ai/</c> and strips the prefix, so
/// <c>api/v1/ai</c> resolves inside the module either way. This host answers bare objects (no success
/// envelope), which <see cref="ModuleApiClient"/> passes through unchanged.
/// </remarks>
public sealed class AiApi
{
    private const string BasePath = "api/v1/ai";

    private readonly ModuleApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="AiApi"/> class.</summary>
    /// <param name="api">The shared frame API client.</param>
    public AiApi(ModuleApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <summary>Lists the caller's conversations, most recently changed first.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversations.</returns>
    public async Task<IReadOnlyList<AiConversationDto>> ListConversationsAsync(CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<AiConversationDto>>(
            HttpMethod.Get,
            $"{BasePath}/conversations",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Gets a conversation with its messages.</summary>
    /// <param name="conversationId">The conversation id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation, or <see langword="null"/> when the host has none.</returns>
    public Task<AiConversationDto?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _api.SendAsync<AiConversationDto>(
            HttpMethod.Get,
            $"{BasePath}/conversations/{conversationId}",
            cancellationToken: cancellationToken);

    /// <summary>Creates a conversation.</summary>
    /// <param name="title">The title.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created conversation.</returns>
    public Task<AiConversationDto?> CreateConversationAsync(string title, CancellationToken cancellationToken = default) =>
        _api.SendAsync<AiConversationDto>(
            HttpMethod.Post,
            $"{BasePath}/conversations",
            new { title, systemPrompt = (string?)null },
            cancellationToken);

    /// <summary>Renames a conversation.</summary>
    /// <param name="conversationId">The conversation id.</param>
    /// <param name="title">The new title.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the rename.</returns>
    public async Task RenameConversationAsync(Guid conversationId, string title, CancellationToken cancellationToken = default)
    {
        // PATCH has no object-body overload on the shared client (its helper list is GET/POST/PUT/
        // DELETE shaped), so the JSON content is supplied directly.
        await _api.SendContentAsync<object>(
            HttpMethod.Patch,
            $"{BasePath}/conversations/{conversationId}/title",
            JsonContent.Create(new { title }),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes a conversation.</summary>
    /// <param name="conversationId">The conversation id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the deletion.</returns>
    public Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Delete, $"{BasePath}/conversations/{conversationId}", cancellationToken: cancellationToken);

    /// <summary>Gets the resolved AI settings.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The settings, or <see langword="null"/> when the host could not resolve them.</returns>
    public Task<AiSettingsDto?> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        _api.SendAsync<AiSettingsDto>(HttpMethod.Get, $"{BasePath}/settings", cancellationToken: cancellationToken);

    /// <summary>Checks whether the configured language model is reachable.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the host reported it healthy.</returns>
    public async Task<bool> IsModelHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _api.SendForElementAsync(HttpMethod.Get, $"{BasePath}/health/ollama", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (ModuleApiException)
        {
            // A 503 is the host's "unhealthy" answer; anything else means the probe itself failed.
            return false;
        }
    }

    /// <summary>Builds the streaming endpoint for a conversation.</summary>
    /// <param name="conversationId">The conversation id.</param>
    /// <returns>A URL relative to the frame document.</returns>
    public static string StreamUrl(Guid conversationId) =>
        $"{BasePath}/conversations/{conversationId}/messages/stream";
}
