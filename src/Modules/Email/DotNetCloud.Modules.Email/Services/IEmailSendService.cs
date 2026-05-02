using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Modules.Email.Services;

/// <summary>
/// Service for composing and sending emails.
/// </summary>
public interface IEmailSendService
{
    /// <summary>Sends an email from the specified account.</summary>
    Task SendAsync(Guid accountId, EmailSendRequest request, CallerContext caller, CancellationToken ct = default);
}
