using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Modules.Email.Models;

namespace DotNetCloud.Modules.Email.Services;

/// <summary>
/// HTTP implementation of <see cref="IEmailApiClient"/>.
/// </summary>
public sealed class EmailApiClient : IEmailApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EmailApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // ── Accounts ─────────────────────────────────────────────

    public async Task<IReadOnlyList<EmailAccount>> ListAccountsAsync(CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<EmailAccount>>("api/v1/email/accounts", ct) ?? [];

    public Task<EmailAccount?> GetAccountAsync(Guid id, CancellationToken ct = default)
        => ReadDataAsync<EmailAccount>($"api/v1/email/accounts/{id}", ct);

    public async Task<EmailAccount?> CreateAccountAsync(CreateEmailAccountRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/email/accounts", request, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<EmailAccount>(response, ct);
    }

    public async Task<EmailAccount?> UpdateAccountAsync(Guid id, UpdateEmailAccountRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/v1/email/accounts/{id}", request, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<EmailAccount>(response, ct);
    }

    public async Task DeleteAccountAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/email/accounts/{id}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Mailboxes ────────────────────────────────────────────

    public async Task<IReadOnlyList<EmailMailbox>> ListMailboxesAsync(Guid accountId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<EmailMailbox>>($"api/v1/email/accounts/{accountId}/mailboxes", ct) ?? [];

    // ── Threads ──────────────────────────────────────────────

    public async Task<IReadOnlyList<EmailThread>> ListThreadsAsync(Guid accountId, Guid mailboxId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<EmailThread>>($"api/v1/email/accounts/{accountId}/mailboxes/{mailboxId}/threads", ct) ?? [];

    public async Task<IReadOnlyList<EmailMessage>> ListThreadMessagesAsync(Guid threadId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<EmailMessage>>($"api/v1/email/threads/{threadId}/messages", ct) ?? [];

    // ── Messages ─────────────────────────────────────────────

    public async Task<string?> GetMessageBodyAsync(Guid messageId, CancellationToken ct = default)
    {
        var result = await ReadDataAsync<MessageBodyResult>($"api/v1/email/messages/{messageId}/body", ct);
        return result?.BodyHtml;
    }

    private sealed record MessageBodyResult
    {
        public string? BodyHtml { get; init; }
    }

    // ── Send ─────────────────────────────────────────────────

    public async Task SendAsync(Guid accountId, EmailSendRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/email/accounts/{accountId}/send", request, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Sync ─────────────────────────────────────────────────

    public async Task TriggerSyncAsync(Guid accountId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/email/accounts/{accountId}/sync", null, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Gmail OAuth status ───────────────────────────────────
    // The OAuth flow itself uses full page redirects (browser navigates to
    // /api/v1/email/gmail/oauth/start, Google redirects back to .../complete).
    // This method just checks if OAuth is configured so the UI can show/hide
    // the "Connect Gmail" button.

    public async Task<bool> CheckGmailOAuthConfiguredAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/v1/email/gmail/oauth/status", ct);
            var result = await ReadDataFromResponseAsync<GmailOAuthStatusResult>(response, ct);
            return result?.Configured ?? false;
        }
        catch
        {
            return false;
        }
    }

    private sealed record GmailOAuthStatusResult { public bool Configured { get; init; } }

    // ── Rules ────────────────────────────────────────────────

    public async Task<IReadOnlyList<EmailRule>> ListRulesAsync(Guid? accountId = null, CancellationToken ct = default)
    {
        var url = "api/v1/email/rules";
        if (accountId.HasValue) url += $"?accountId={accountId.Value}";
        return await ReadDataAsync<IReadOnlyList<EmailRule>>(url, ct) ?? [];
    }

    public Task<EmailRule?> GetRuleAsync(Guid id, CancellationToken ct = default)
        => ReadDataAsync<EmailRule>($"api/v1/email/rules/{id}", ct);

    public async Task<EmailRule?> CreateRuleAsync(CreateEmailRuleRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/email/rules", request, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<EmailRule>(response, ct);
    }

    public async Task<EmailRule?> UpdateRuleAsync(Guid id, UpdateEmailRuleRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/email/rules/{id}", request, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<EmailRule>(response, ct);
    }

    public async Task DeleteRuleAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/email/rules/{id}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<int> RunRulesAsync(Guid? accountId = null, Guid? mailboxId = null, CancellationToken ct = default)
    {
        var url = "api/v1/email/rules/run";
        if (accountId.HasValue || mailboxId.HasValue)
        {
            url += "?";
            if (accountId.HasValue) url += $"accountId={accountId.Value}&";
            if (mailboxId.HasValue) url += $"mailboxId={mailboxId.Value}&";
            url = url.TrimEnd('&');
        }
        var response = await _httpClient.PostAsync(url, null, ct);
        await EnsureSuccessOrThrowAsync(response);
        var result = await ReadDataFromResponseAsync<JsonElement>(response, ct);
        return result.TryGetProperty("executed", out var prop) ? prop.GetInt32() : 0;
    }

    // ── Helpers ──────────────────────────────────────────────

    private async Task<T?> ReadDataAsync<T>(string url, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<T>(response, ct);
    }

    private static async Task<T?> ReadDataFromResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(json);

        if (!TryGetPropertyIgnoreCase(document.RootElement, "data", out var dataElement))
            return default;

        if (dataElement.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(dataElement, "data", out var nestedData))
            dataElement = nestedData;

        return JsonSerializer.Deserialize<T>(dataElement.GetRawText(), JsonOptions);
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        string message;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (TryGetPropertyIgnoreCase(doc.RootElement, "error", out var error) &&
                TryGetPropertyIgnoreCase(error, "message", out var msg))
                message = msg.GetString() ?? $"Request failed ({(int)response.StatusCode}).";
            else
                message = $"Request failed ({(int)response.StatusCode}).";
        }
        catch
        {
            message = $"Request failed ({(int)response.StatusCode}).";
        }

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

/// <summary>
/// Extension method to make PATCH requests with JSON body.
/// </summary>
file static class HttpClientPatchExtensions
{
    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string? requestUri, T value, CancellationToken ct = default)
    {
        var content = JsonContent.Create(value, options: new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return client.PatchAsync(requestUri, content, ct);
    }
}
