namespace DotNetCloud.Modules.Email.Host.Services;

/// <summary>
/// gRPC service for Email module business operations.
/// </summary>
public sealed class EmailGrpcService : EmailService.EmailServiceBase
{
    private readonly ILogger<EmailGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailGrpcService"/> class.
    /// </summary>
    public EmailGrpcService(ILogger<EmailGrpcService> logger)
    {
        _logger = logger;
    }
}
