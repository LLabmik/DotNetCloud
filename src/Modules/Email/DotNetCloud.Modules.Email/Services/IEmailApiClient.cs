using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;

namespace DotNetCloud.Modules.Email.Services;

/// <summary>
/// HTTP API client for Email REST endpoints.
/// </summary>
public interface IEmailApiClient
{
    // Accounts
    Task<IReadOnlyList<EmailAccount>> ListAccountsAsync(CancellationToken ct = default);
    Task<EmailAccount?> GetAccountAsync(Guid id, CancellationToken ct = default);
    Task<EmailAccount?> CreateAccountAsync(CreateEmailAccountRequest request, CancellationToken ct = default);
    Task<EmailAccount?> UpdateAccountAsync(Guid id, UpdateEmailAccountRequest request, CancellationToken ct = default);
    Task DeleteAccountAsync(Guid id, CancellationToken ct = default);

    // Send
    Task SendAsync(Guid accountId, EmailSendRequest request, CancellationToken ct = default);

    // Sync
    Task TriggerSyncAsync(Guid accountId, CancellationToken ct = default);

    // Gmail OAuth
    Task<GmailOAuthStartResult?> StartGmailOAuthAsync(CancellationToken ct = default);
    Task<EmailAccount?> CompleteGmailOAuthAsync(string state, string code, CancellationToken ct = default);

    // Rules
    Task<IReadOnlyList<EmailRule>> ListRulesAsync(Guid? accountId = null, CancellationToken ct = default);
    Task<EmailRule?> GetRuleAsync(Guid id, CancellationToken ct = default);
    Task<EmailRule?> CreateRuleAsync(CreateEmailRuleRequest request, CancellationToken ct = default);
    Task<EmailRule?> UpdateRuleAsync(Guid id, UpdateEmailRuleRequest request, CancellationToken ct = default);
    Task DeleteRuleAsync(Guid id, CancellationToken ct = default);
    Task<int> RunRulesAsync(Guid? accountId = null, Guid? mailboxId = null, CancellationToken ct = default);
}

/// <summary>
/// Result of starting Gmail OAuth flow.
/// </summary>
public sealed record GmailOAuthStartResult
{
    public string AuthorizationUrl { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
}
