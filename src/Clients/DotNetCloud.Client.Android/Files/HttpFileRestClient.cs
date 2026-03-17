using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Files;

/// <summary>
/// <see cref="IFileRestClient"/> implementation backed by <see cref="HttpClient"/>.
/// Registered via <c>AddHttpClient&lt;IFileRestClient, HttpFileRestClient&gt;()</c>.
/// </summary>
internal sealed class HttpFileRestClient : IFileRestClient
{
    private const int MaxChunkSize = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly ILogger<HttpFileRestClient> _logger;

    /// <summary>Initializes a new <see cref="HttpFileRestClient"/>.</summary>
    public HttpFileRestClient(HttpClient http, ILogger<HttpFileRestClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ── Browsing ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileItem>> ListChildrenAsync(
        string serverBaseUrl, string accessToken, Guid? folderId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var path = folderId.HasValue
            ? $"{Url(serverBaseUrl)}/api/v1/files?parentId={folderId}"
            : $"{Url(serverBaseUrl)}/api/v1/files";
        var nodes = await GetEnvelopeDataAsync<List<FileNodeDto>>(path, ct).ConfigureAwait(false);
        return (nodes ?? []).Select(ToFileItem).ToList();
    }

    /// <inheritdoc />
    public async Task<FileItem> GetNodeAsync(
        string serverBaseUrl, string accessToken, Guid nodeId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var node = await GetEnvelopeDataAsync<FileNodeDto>(
            $"{Url(serverBaseUrl)}/api/v1/files/{nodeId}", ct).ConfigureAwait(false);
        return node is null
            ? throw new InvalidOperationException($"Node {nodeId} not found.")
            : ToFileItem(node);
    }

    // ── Folder Operations ────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<FileItem> CreateFolderAsync(
        string serverBaseUrl, string accessToken,
        string name, Guid? parentId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PostAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/files/folders",
            new { name, parentId }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var node = await ReadEnvelopeDataAsync<FileNodeDto>(response, ct).ConfigureAwait(false);
        return node is null
            ? throw new InvalidOperationException("Server returned null for folder creation.")
            : ToFileItem(node);
    }

    /// <inheritdoc />
    public async Task<FileItem> RenameAsync(
        string serverBaseUrl, string accessToken,
        Guid nodeId, string newName, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PutAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/files/{nodeId}/rename",
            new { name = newName }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var node = await ReadEnvelopeDataAsync<FileNodeDto>(response, ct).ConfigureAwait(false);
        return node is null
            ? throw new InvalidOperationException("Server returned null for rename.")
            : ToFileItem(node);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string serverBaseUrl, string accessToken,
        Guid nodeId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.DeleteAsync(
            $"{Url(serverBaseUrl)}/api/v1/files/{nodeId}", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    // ── Upload ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<FileItem> UploadFileAsync(
        string serverBaseUrl, string accessToken,
        string fileName, Guid? parentId,
        Stream fileData, long fileSize, string? mimeType,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var baseUrl = Url(serverBaseUrl);

        // Read entire stream into memory for chunking (files on Android are typically < 100 MB).
        using var ms = new MemoryStream();
        await fileData.CopyToAsync(ms, ct).ConfigureAwait(false);
        var data = ms.ToArray();

        var chunks = SplitIntoChunks(data);
        var chunkHashes = chunks.Select(c => Convert.ToHexStringLower(SHA256.HashData(c))).ToList();

        // 1. Initiate upload session
        var initiateBody = new { fileName, parentId, totalSize = (long)data.Length, mimeType, chunkHashes };
        using var initiateResp = await _http
            .PostAsJsonAsync($"{baseUrl}/api/v1/files/upload/initiate", initiateBody, ct)
            .ConfigureAwait(false);
        initiateResp.EnsureSuccessStatusCode();
        var session = await ReadEnvelopeDataAsync<InitiateUploadDto>(initiateResp, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty initiate response.");

        var existing = new HashSet<string>(session.ExistingChunks ?? [], StringComparer.OrdinalIgnoreCase);
        long transferred = 0;

        // 2. Upload missing chunks
        for (int i = 0; i < chunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var hash = chunkHashes[i];

            if (!existing.Contains(hash))
            {
                using var chunkContent = new ByteArrayContent(chunks[i]);
                chunkContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                using var chunkResp = await _http
                    .PutAsync($"{baseUrl}/api/v1/files/upload/{session.SessionId}/chunks/{hash}", chunkContent, ct)
                    .ConfigureAwait(false);
                chunkResp.EnsureSuccessStatusCode();
            }

            transferred += chunks[i].Length;
            progress?.Report(new FileTransferProgress(transferred, data.Length));
        }

        // 3. Complete upload
        using var completeResp = await _http
            .PostAsync($"{baseUrl}/api/v1/files/upload/{session.SessionId}/complete", null, ct)
            .ConfigureAwait(false);
        completeResp.EnsureSuccessStatusCode();
        var node = await ReadEnvelopeDataAsync<FileNodeDto>(completeResp, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty complete response.");

        _logger.LogInformation("Uploaded {FileName} ({Size} bytes, {Chunks} chunks).",
            fileName, data.Length, chunks.Count);

        return ToFileItem(node);
    }

    // ── Download ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Stream> DownloadAsync(
        string serverBaseUrl, string accessToken,
        Guid nodeId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var response = await _http.GetAsync(
            $"{Url(serverBaseUrl)}/api/v1/files/{nodeId}/download",
            HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }

    // ── Quota ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<QuotaInfo> GetQuotaAsync(
        string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var dto = await GetEnvelopeDataAsync<QuotaDto>(
            $"{Url(serverBaseUrl)}/api/v1/files/quota", ct).ConfigureAwait(false);
        return dto is null
            ? new QuotaInfo(0, 0)
            : new QuotaInfo(dto.UsedBytes, dto.QuotaBytes);
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void SetAuth(string accessToken) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

    private static string Url(string serverBaseUrl) => serverBaseUrl.TrimEnd('/');

    private async Task<T?> GetEnvelopeDataAsync<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<T>(response, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the response body, unwrapping the server's standard envelope
    /// (<c>{"success":true,"data":...}</c>) if present.
    /// </summary>
    private static async Task<T?> ReadEnvelopeDataAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("data", out var dataProp))
        {
            return dataProp.Deserialize<T>(JsonOpts);
        }

        return doc.RootElement.Deserialize<T>(JsonOpts);
    }

    private static List<byte[]> SplitIntoChunks(byte[] data)
    {
        var chunks = new List<byte[]>();
        int offset = 0;
        while (offset < data.Length)
        {
            int size = Math.Min(MaxChunkSize, data.Length - offset);
            var chunk = new byte[size];
            Array.Copy(data, offset, chunk, 0, size);
            chunks.Add(chunk);
            offset += size;
        }
        return chunks.Count > 0 ? chunks : [data];
    }

    private static FileItem ToFileItem(FileNodeDto d) => new(
        d.Id, d.Name, d.NodeType, d.Size, d.MimeType, d.ParentId, d.UpdatedAt, d.ChildCount);

    // ── DTOs ─────────────────────────────────────────────────────────

    private sealed class FileNodeDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string NodeType { get; init; } = "File";
        public long Size { get; init; }
        public string? MimeType { get; init; }
        public Guid? ParentId { get; init; }
        public DateTime UpdatedAt { get; init; }
        public int ChildCount { get; init; }
    }

    private sealed class InitiateUploadDto
    {
        public string SessionId { get; init; } = string.Empty;
        [JsonPropertyName("existingChunks")]
        public IReadOnlyList<string>? ExistingChunks { get; init; }
    }

    private sealed class QuotaDto
    {
        public long UsedBytes { get; init; }
        public long QuotaBytes { get; init; }
    }
}
