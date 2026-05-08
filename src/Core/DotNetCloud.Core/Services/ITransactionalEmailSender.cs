namespace DotNetCloud.Core.Services;

/// <summary>
/// Sends transactional system emails (confirmation, password reset, etc.).
/// </summary>
/// <remarks>
/// This abstraction lives in Core so that the Auth module can send emails without
/// depending on the optional Email module. If the Email module is installed, it may
/// replace this implementation with its own.
/// </remarks>
public interface ITransactionalEmailSender
{
    /// <summary>
    /// Sends a transactional HTML email.
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="toName">Recipient display name.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML body content.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default);
}
