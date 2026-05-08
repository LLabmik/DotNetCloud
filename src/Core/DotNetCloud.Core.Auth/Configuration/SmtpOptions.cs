namespace DotNetCloud.Core.Auth.Configuration;

/// <summary>
/// SMTP configuration for transactional email sending.
/// </summary>
/// <remarks>
/// Bind from the <c>"Smtp"</c> configuration section.
/// When <see cref="Host"/> is empty, email sending is silently skipped.
/// </remarks>
public sealed class SmtpOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Smtp";

    /// <summary>
    /// Gets or sets the SMTP server hostname. Empty disables sending.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SMTP server port. Defaults to 587.
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// Gets or sets whether to use STARTTLS. Defaults to <see langword="true"/>.
    /// </summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>
    /// Gets or sets the SMTP authentication username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SMTP authentication password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender email address. Defaults to <c>noreply@dotnetcloud.local</c>.
    /// </summary>
    public string FromEmail { get; set; } = "noreply@dotnetcloud.local";

    /// <summary>
    /// Gets or sets the sender display name. Defaults to <c>DotNetCloud</c>.
    /// </summary>
    public string FromName { get; set; } = "DotNetCloud";

    /// <summary>
    /// Gets or sets the public base URL used to construct links in emails.
    /// </summary>
    public string BaseUrl { get; set; } = "https://localhost:5001";
}
