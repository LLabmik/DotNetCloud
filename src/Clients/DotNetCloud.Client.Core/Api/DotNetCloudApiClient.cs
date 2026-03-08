using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// HTTP implementation of <see cref="IDotNetCloudApiClient"/> with retry and rate-limit handling.
/// </summary>
public sealed class DotNetCloudApiClient : IDotNetCloudApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 500;

    private readonly HttpClient _http;
    private readonly ILogger<DotNetCloudApiClient> _logger;

    /// <summary>Access token injected per-request.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Initializes a new instance of <see cref="DotNetCloudApiClient"/>.</summary>
    public DotNetCloudApiClient(HttpClient httpClient, ILogger<DotNetCloudApiClient> logger)
    {
        _http = httpClient;
        _logger = logger;
    }

    // ── Authentication ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
        };
        return await PostFormAsync<TokenResponse>("connect/token", form, withAuth: false, cancellationToken)
               ?? throw new InvalidOperationException("Empty token response.");
    }

    /// <inheritdoc/>
    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, string clientId, CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken,
        };

        using var response = await SendWithRetryAsync(
            () => CreateFormRequest(HttpMethod.Post, "connect/token", form, withAuth: false),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Token refresh failed ({(int)response.StatusCode}): {body}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Empty token response.");
    }

    /// <inheritdoc/>
    public async Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string> { ["token"] = token };
        using var response = await SendWithRetryAsync(
            () => CreateFormRequest(HttpMethod.Post, "connect/revocation", form, withAuth: false),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ── File Operations ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FileNodeResponse>> ListChildrenAsync(Guid? folderId, CancellationToken cancellationToken = default)
    {
        var path = folderId.HasValue
            ? $"api/v1/files/{folderId}/children"
            : "api/v1/files/root/children";
        return await GetAsync<List<FileNodeResponse>>(path, cancellationToken) ?? [];
    }

    /// <inheritdoc/>
    public async Task<FileNodeResponse> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default) =>
        await GetAsync<FileNodeResponse>($"api/v1/files/{nodeId}", cancellationToken)
        ?? throw new InvalidOperationException($"Node {nodeId} not found.");

    /// <inheritdoc/>
    public async Task<FileNodeResponse> CreateFolderAsync(string name, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var body = new { name, parentId };
        return await PostJsonAsync<FileNodeResponse>("api/v1/files/folders", body, cancellationToken)
               ?? throw new InvalidOperationException("Server returned null for folder creation.");
    }

    /// <inheritdoc/>
    public async Task<FileNodeResponse> RenameAsync(Guid nodeId, string newName, CancellationToken cancellationToken = default)
    {
        var body = new { name = newName };
        return await PutJsonAsync<FileNodeResponse>($"api/v1/files/{nodeId}/rename", body, cancellationToken)
               ?? throw new InvalidOperationException("Server returned null for rename.");
    }

    /// <inheritdoc/>
    public async Task<FileNodeResponse> MoveAsync(Guid nodeId, Guid targetParentId, CancellationToken cancellationToken = default)
    {
        var body = new { targetParentId };
        return await PutJsonAsync<FileNodeResponse>($"api/v1/files/{nodeId}/move", body, cancellationToken)
               ?? throw new InvalidOperationException("Server returned null for move.");
    }

    /// <inheritdoc/>
    public async Task<FileNodeResponse> CopyAsync(Guid nodeId, Guid targetParentId, CancellationToken cancellationToken = default)
    {
        var body = new { targetParentId };
        return await PostJsonAsync<FileNodeResponse>($"api/v1/files/{nodeId}/copy", body, cancellationToken)
               ?? throw new InvalidOperationException("Server returned null for copy.");
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
            () => CreateAuthenticatedRequest(HttpMethod.Delete, $"api/v1/files/{nodeId}"),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ── Upload Operations ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<UploadSessionResponse> InitiateUploadAsync(
        string fileName, Guid? parentId, long totalSize, string? mimeType,
        IReadOnlyList<string> chunkHashes, CancellationToken cancellationToken = default)
    {
        var body = new { fileName, parentId, totalSize, mimeType, chunkHashes };
        return await PostJsonAsync<UploadSessionResponse>("api/v1/files/upload/initiate", body, cancellationToken)
               ?? throw new InvalidOperationException("Server returned null for upload initiation.");
    }

    /// <inheritdoc/>
    public async Task UploadChunkAsync(Guid sessionId, int chunkIndex, string chunkHash, Stream chunkData, CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, $"api/v1/files/upload/{sessionId}/chunks/{chunkIndex}");
        request.Content = new StreamContent(chunkData);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Headers.Add("X-Chunk-Hash", chunkHash);
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task<CompleteUploadResponse> CompleteUploadAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await PostJsonAsync<CompleteUploadResponse>($"api/v1/files/upload/{sessionId}/complete", new { }, cancellationToken)
               ?? throw new InvalidOperationException("Server returned null for upload completion.");
    }

    // ── Download Operations ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Stream> DownloadAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var response = await SendWithRetryAsync(
            () => CreateAuthenticatedRequest(HttpMethod.Get, $"api/v1/files/{nodeId}/download"),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Stream> DownloadVersionAsync(Guid nodeId, int versionNumber, CancellationToken cancellationToken = default)
    {
        var response = await SendWithRetryAsync(
            () => CreateAuthenticatedRequest(HttpMethod.Get, $"api/v1/files/{nodeId}/download?version={versionNumber}"),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Stream> DownloadChunkByHashAsync(string chunkHash, CancellationToken cancellationToken = default)
    {
        var response = await SendWithRetryAsync(
            () => CreateAuthenticatedRequest(HttpMethod.Get, $"api/v1/files/chunks/{chunkHash}"),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ChunkManifestResponse> GetChunkManifestAsync(Guid nodeId, CancellationToken cancellationToken = default) =>
        await GetAsync<ChunkManifestResponse>($"api/v1/files/{nodeId}/chunks", cancellationToken)
        ?? new ChunkManifestResponse();

    // ── Sync Operations ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncChangeResponse>> GetChangesSinceAsync(DateTime since, Guid? folderId, CancellationToken cancellationToken = default)
    {
        var query = $"since={Uri.EscapeDataString(since.ToString("O"))}";
        if (folderId.HasValue)
            query += $"&folderId={folderId}";
        return await GetAsync<List<SyncChangeResponse>>($"api/v1/files/sync/changes?{query}", cancellationToken) ?? [];
    }

    /// <inheritdoc/>
    public async Task<SyncTreeNodeResponse> GetFolderTreeAsync(Guid? folderId, CancellationToken cancellationToken = default)
    {
        var path = folderId.HasValue
            ? $"api/v1/files/sync/tree?folderId={folderId}"
            : "api/v1/files/sync/tree";
        return await GetAsync<SyncTreeNodeResponse>(path, cancellationToken)
               ?? new SyncTreeNodeResponse { NodeId = Guid.Empty, Name = "root", NodeType = "Folder" };
    }

    /// <inheritdoc/>
    public async Task<ReconcileResponse> ReconcileAsync(Guid? folderId, IReadOnlyList<ClientNodeEntry> clientNodes, CancellationToken cancellationToken = default)
    {
        var body = new { folderId, clientNodes };
        return await PostJsonAsync<ReconcileResponse>("api/v1/files/sync/reconcile", body, cancellationToken)
               ?? new ReconcileResponse();
    }

    // ── Quota Operations ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<QuotaResponse> GetQuotaAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<QuotaResponse>("api/v1/files/quota", cancellationToken)
        ?? new QuotaResponse { UserId = Guid.Empty };

    // ── Private Helpers ─────────────────────────────────────────────────────

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => CreateAuthenticatedRequest(HttpMethod.Get, path),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var wwwAuth = response.Headers.WwwAuthenticate.ToString();
            _logger.LogError("HTTP {Status} on GET {Path}. WWW-Authenticate: {WwwAuth}. Body: {Body}",
                (int)response.StatusCode, path, wwwAuth, body);
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<T?> PostJsonAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () =>
            {
                var req = CreateAuthenticatedRequest(HttpMethod.Post, path);
                req.Content = JsonContent.Create(body, options: JsonOptions);
                return req;
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<T?> PutJsonAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () =>
            {
                var req = CreateAuthenticatedRequest(HttpMethod.Put, path);
                req.Content = JsonContent.Create(body, options: JsonOptions);
                return req;
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<T?> PostFormAsync<T>(string path, Dictionary<string, string> form, bool withAuth, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => CreateFormRequest(HttpMethod.Post, path, form, withAuth),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (AccessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        return request;
    }

    private static HttpRequestMessage CreateFormRequest(HttpMethod method, string path, Dictionary<string, string> form, bool withAuth)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new FormUrlEncodedContent(form),
        };
        return request;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            using var request = requestFactory();
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "HTTP request failed (attempt {Attempt}/{Max}), retrying.", attempt + 1, MaxRetries);
                await DelayAsync(attempt, null, cancellationToken);
                continue;
            }

            // Handle rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                _logger.LogWarning("Rate limited (429). Waiting {Delay}s before retry.", retryAfter.TotalSeconds);
                if (attempt >= MaxRetries)
                    return response;
                response.Dispose();
                await Task.Delay(retryAfter, cancellationToken);
                continue;
            }

            // Retry on 5xx (server errors) but not 4xx (client errors)
            if ((int)response.StatusCode >= 500 && attempt < MaxRetries)
            {
                _logger.LogWarning("Server error {Status} (attempt {Attempt}/{Max}), retrying.", response.StatusCode, attempt + 1, MaxRetries);
                response.Dispose();
                await DelayAsync(attempt, null, cancellationToken);
                continue;
            }

            return response;
        }

        // Unreachable, but satisfies compiler
        return await _http.SendAsync(requestFactory(), cancellationToken);
    }

    private static async Task DelayAsync(int attempt, TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        var delay = retryAfter ?? TimeSpan.FromMilliseconds(BaseDelayMs * Math.Pow(2, attempt));
        await Task.Delay(delay, cancellationToken);
    }
}
