using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Email.Models;

namespace DotNetCloud.Modules.Email.Services;

/// <summary>
/// Service for managing email accounts.
/// </summary>
public interface IEmailAccountService
{
    /// <summary>Lists accounts for the caller.</summary>
    Task<IReadOnlyList<EmailAccount>> ListAsync(CallerContext caller, CancellationToken ct = default);

    /// <summary>Gets an account by ID.</summary>
    Task<EmailAccount?> GetAsync(Guid id, CallerContext caller, CancellationToken ct = default);

    /// <summary>Creates a new email account.</summary>
    Task<EmailAccount> CreateAsync(CreateEmailAccountRequest request, CallerContext caller, CancellationToken ct = default);

    /// <summary>Updates an account (enable/disable, rename).</summary>
    Task<EmailAccount> UpdateAsync(Guid id, UpdateEmailAccountRequest request, CallerContext caller, CancellationToken ct = default);

    /// <summary>Deletes an account and all associated data.</summary>
    Task DeleteAsync(Guid id, CallerContext caller, CancellationToken ct = default);
}

/// <summary>Request DTO for creating an email account.</summary>
public sealed record CreateEmailAccountRequest
{
    /// <summary>Provider type.</summary>
    public required EmailProviderType ProviderType { get; init; }

    /// <summary>Display name for the account.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Email address.</summary>
    public required string EmailAddress { get; init; }

    /// <summary>Provider-specific credentials (IMAP/SMTP config or Gmail OAuth state).</summary>
    public required string? CredentialsJson { get; init; }
}

/// <summary>Request DTO for updating an email account.</summary>
public sealed record UpdateEmailAccountRequest
{
    /// <summary>Updated display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Updated enabled state.</summary>
    public bool? IsEnabled { get; init; }
}
