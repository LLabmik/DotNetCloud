using System.Net.Http.Headers;
using System.Text.Json;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.Email.Web.Services;

/// <summary>
/// The Email module UI's client for its own host's REST API.
/// </summary>
/// <remarks>
/// URLs are relative: the core proxies <c>/apps/dotnetcloud.email/</c> and strips the prefix, so
/// <c>api/v1/email</c> resolves inside the module either way. This host answers the standard success
/// envelope, which <see cref="ModuleApiClient"/> unwraps, and it binds enums as numbers — the DTOs
/// therefore carry <see cref="int"/> where the domain has an enum.
/// </remarks>
public sealed class EmailApi
{
    private const string BasePath = "api/v1/email";

    private readonly ModuleApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="EmailApi"/> class.</summary>
    /// <param name="api">The shared frame API client.</param>
    public EmailApi(ModuleApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <summary>Lists the caller's email accounts.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The accounts.</returns>
    public async Task<IReadOnlyList<EmailAccountDto>> ListAccountsAsync(CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<EmailAccountDto>>(HttpMethod.Get, $"{BasePath}/accounts", cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? [];

    /// <summary>Creates an IMAP/SMTP account.</summary>
    /// <param name="displayName">The display name.</param>
    /// <param name="emailAddress">The email address.</param>
    /// <param name="credentials">The connection settings, serialised as JSON.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the account.</returns>
    public Task CreateImapAccountAsync(
        string displayName,
        string emailAddress,
        object credentials,
        CancellationToken cancellationToken = default) =>
        _api.SendAsync(
            HttpMethod.Post,
            $"{BasePath}/accounts",
            new
            {
                providerType = 0,
                displayName,
                emailAddress,
                credentialsJson = JsonSerializer.Serialize(credentials),
            },
            cancellationToken);

    /// <summary>Deletes an account and its synced messages.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the deletion.</returns>
    public Task DeleteAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Delete, $"{BasePath}/accounts/{accountId}", cancellationToken: cancellationToken);

    /// <summary>Starts a sync for an account.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the request.</returns>
    public Task SyncAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Post, $"{BasePath}/accounts/{accountId}/sync", cancellationToken: cancellationToken);

    /// <summary>Lists an account's mailboxes.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mailboxes.</returns>
    public async Task<IReadOnlyList<EmailMailboxDto>> ListMailboxesAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<EmailMailboxDto>>(
            HttpMethod.Get,
            $"{BasePath}/accounts/{accountId}/mailboxes",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Lists the threads in one mailbox.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="mailboxId">The mailbox id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The threads.</returns>
    public async Task<IReadOnlyList<EmailThreadDto>> ListThreadsAsync(
        Guid accountId,
        Guid mailboxId,
        CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<EmailThreadDto>>(
            HttpMethod.Get,
            $"{BasePath}/accounts/{accountId}/mailboxes/{mailboxId}/threads",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Lists a thread's messages.</summary>
    /// <param name="threadId">The thread id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The messages.</returns>
    public async Task<IReadOnlyList<EmailMessageDto>> ListMessagesAsync(
        Guid threadId,
        CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<EmailMessageDto>>(
            HttpMethod.Get,
            $"{BasePath}/threads/{threadId}/messages",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Gets a message's HTML body.</summary>
    /// <param name="messageId">The message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The body HTML, or <see langword="null"/> when the host had none.</returns>
    public async Task<string?> GetBodyHtmlAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        (await _api.SendAsync<EmailMessageBodyDto>(
            HttpMethod.Get,
            $"{BasePath}/messages/{messageId}/body",
            cancellationToken: cancellationToken).ConfigureAwait(false))?.BodyHtml;

    /// <summary>Sends a message.</summary>
    /// <param name="accountId">The account to send from.</param>
    /// <param name="payload">The message body, as the host expects it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the message.</returns>
    public Task SendAsync(Guid accountId, object payload, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Post, $"{BasePath}/accounts/{accountId}/send", payload, cancellationToken);

    /// <summary>Uploads a staged attachment.</summary>
    /// <param name="content">The file content.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The staged attachment.</returns>
    public async Task<EmailAttachmentDto> UploadAttachmentAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(file, "file", fileName);

        return await _api.SendContentAsync<EmailAttachmentDto>(
            HttpMethod.Post,
            $"{BasePath}/upload-attachment",
            form,
            cancellationToken).ConfigureAwait(false) ?? new EmailAttachmentDto { FileName = fileName, ContentType = contentType };
    }

    /// <summary>Copies one of the caller's cloud files into the attachment store.</summary>
    /// <param name="nodeId">The file id.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="size">The size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The staged attachment.</returns>
    public async Task<EmailAttachmentDto> AttachFromFileAsync(
        Guid nodeId,
        string fileName,
        string? contentType,
        long size,
        CancellationToken cancellationToken = default) =>
        await _api.SendAsync<EmailAttachmentDto>(
            HttpMethod.Post,
            $"{BasePath}/attachments/from-file",
            new { nodeId, fileName, contentType = contentType ?? "application/octet-stream", size },
            cancellationToken).ConfigureAwait(false) ?? new EmailAttachmentDto { FileName = fileName };

    /// <summary>Copies a received attachment into the caller's cloud files.</summary>
    /// <param name="attachmentId">The attachment id.</param>
    /// <param name="parentId">The destination folder, or <see langword="null"/> for the files root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host wrote the file.</returns>
    public Task SaveAttachmentToFilesAsync(Guid attachmentId, Guid? parentId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(
            HttpMethod.Post,
            $"{BasePath}/attachments/{attachmentId}/save-to-files",
            new { parentId },
            cancellationToken);

    /// <summary>Lists one folder of the caller's cloud files.</summary>
    /// <param name="parentId">The folder id, or <see langword="null"/> for the root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder's children.</returns>
    public async Task<IReadOnlyList<EmailFileNodeDto>> ListFilesAsync(
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        var url = parentId is { } id ? $"{BasePath}/files?parentId={id}" : $"{BasePath}/files";
        var element = await _api.SendForElementAsync(HttpMethod.Get, url, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (element is null)
        {
            return [];
        }

        if (ModuleApiClient.TryGetProperty(element.Value, "nodes", out var nodes))
        {
            return _api.Deserialize<List<EmailFileNodeDto>>(nodes) ?? [];
        }

        return _api.Deserialize<List<EmailFileNodeDto>>(element.Value) ?? [];
    }

    /// <summary>Lists the caller's rules.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rules.</returns>
    public async Task<IReadOnlyList<EmailRuleDto>> ListRulesAsync(CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<EmailRuleDto>>(HttpMethod.Get, $"{BasePath}/rules", cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? [];

    /// <summary>Creates a rule.</summary>
    /// <param name="payload">The rule.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the rule.</returns>
    public Task CreateRuleAsync(object payload, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Post, $"{BasePath}/rules", payload, cancellationToken);

    /// <summary>Updates a rule.</summary>
    /// <param name="ruleId">The rule id.</param>
    /// <param name="payload">The rule.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the rule.</returns>
    public Task UpdateRuleAsync(Guid ruleId, object payload, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Put, $"{BasePath}/rules/{ruleId}", payload, cancellationToken);

    /// <summary>Deletes a rule.</summary>
    /// <param name="ruleId">The rule id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the deletion.</returns>
    public Task DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Delete, $"{BasePath}/rules/{ruleId}", cancellationToken: cancellationToken);

    /// <summary>Runs the rules over synced mail.</summary>
    /// <param name="accountId">The account to run for, or <see langword="null"/> for every account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many messages were processed.</returns>
    public async Task<int> RunRulesAsync(Guid? accountId, CancellationToken cancellationToken = default)
    {
        var url = accountId is { } id ? $"{BasePath}/rules/run?accountId={id}" : $"{BasePath}/rules/run";
        var element = await _api.SendForElementAsync(HttpMethod.Post, url, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return element is not null
            && ModuleApiClient.TryGetProperty(element.Value, "executed", out var executed)
            && executed.TryGetInt32(out var count)
                ? count
                : 0;
    }

    /// <summary>Gets whether an administrator configured Gmail OAuth.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the Gmail connect flow is available.</returns>
    public async Task<bool> IsGmailConfiguredAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _api.SendAsync<EmailGmailStatusDto>(
                HttpMethod.Get,
                $"{BasePath}/gmail/oauth/status",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return status?.Configured == true;
        }
        catch (ModuleApiException)
        {
            return false;
        }
    }

    /// <summary>Builds the relative Gmail consent URL the shell opens in its own tab.</summary>
    /// <returns>A URL relative to the frame document.</returns>
    public static string GmailStartUrl => $"{BasePath}/gmail/oauth/start";

    /// <summary>Builds the relative download URL for an attachment.</summary>
    /// <param name="attachmentId">The attachment id.</param>
    /// <returns>A URL relative to the frame document.</returns>
    public static string AttachmentDownloadUrl(Guid attachmentId) =>
        $"{BasePath}/attachments/{attachmentId}/download";
}
