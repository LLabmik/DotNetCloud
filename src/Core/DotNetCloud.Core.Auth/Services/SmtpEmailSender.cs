using DotNetCloud.Core.Auth.Configuration;
using DotNetCloud.Core.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DotNetCloud.Core.Auth.Services;

/// <summary>
/// Sends transactional emails via SMTP using MailKit.
/// </summary>
/// <remarks>
/// Gracefully no-ops when <see cref="SmtpOptions.Host"/> is not configured.
/// MailKit SMTP pattern borrowed from the Email module's <c>ImapSmtpEmailProvider</c>.
/// </remarks>
public sealed class SmtpEmailSender : ITransactionalEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SmtpEmailSender"/>.
    /// </summary>
    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogWarning(
                "SMTP not configured (Smtp:Host is empty). Skipping email to {ToEmail}: {Subject}",
                toEmail, subject);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();

            _logger.LogInformation(
                "SMTP Send: connecting to {Host}:{Port}", _options.Host, _options.Port);
            await client.ConnectAsync(_options.Host, _options.Port,
                SecureSocketOptions.Auto, ct);

            if (!string.IsNullOrEmpty(_options.Username))
            {
                _logger.LogInformation("SMTP Send: authenticating as {Username}", _options.Username);
                await client.AuthenticateAsync(_options.Username, _options.Password, ct);
            }

            _logger.LogInformation("SMTP Send: sending to {ToEmail}", toEmail);
            await client.SendAsync(message, ct);

            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("SMTP Send: completed to {ToEmail} ({Subject})", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} ({Subject})", toEmail, subject);
            throw;
        }
    }
}
