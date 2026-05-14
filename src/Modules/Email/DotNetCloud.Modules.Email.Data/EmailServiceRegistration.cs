using DotNetCloud.Modules.Email.Data.Services;
using DotNetCloud.Modules.Email.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotNetCloud.Modules.Email.Data;

/// <summary>
/// DI registration for the Email module.
/// </summary>
public static class EmailServiceRegistration
{
    /// <summary>
    /// Registers all Email module services.
    /// </summary>
    public static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IEmailAccountService, EmailAccountService>();
        services.AddScoped<IEmailSendService, EmailSendService>();
        services.AddScoped<IEmailRuleService, EmailRuleService>();
        services.AddScoped<IEmailProvider, ImapSmtpEmailProvider>();
        services.AddScoped<IEmailProvider, GmailEmailProvider>();
        services.AddSingleton<EmailCredentialEncryptionService>();
        services.AddSingleton<EmailSyncBackgroundService>();
        services.AddSingleton<IEmailSyncService>(sp => sp.GetRequiredService<EmailSyncBackgroundService>());
        services.AddHostedService(sp => sp.GetRequiredService<EmailSyncBackgroundService>());

        // Attachment storage
        services.Configure<AttachmentStorageOptions>(configuration.GetSection("Email:AttachmentStorage"));
        services.AddScoped<IAttachmentStorage, FileSystemAttachmentStorage>();

        // Temp attachment cleanup background job
        services.AddHostedService<CleanupTempAttachmentsBackgroundService>();

        // HTTP client factory used by EmailController for cross-module calls (e.g. Save to Files).
        // The server uses a self-signed certificate for local development; bypass TLS validation
        // only in Development environment or when Email:FilesApiClient:AllowInsecureTls is explicitly
        // configured to true. In production, proper certificate validation is enforced.
        var allowInsecureTls = configuration.GetValue<bool>("Email:FilesApiClient:AllowInsecureTls");

        services.AddHttpClient("FilesApiInternal")
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var handler = new HttpClientHandler();
                var env = sp.GetRequiredService<IHostEnvironment>();

                if (env.IsDevelopment() || allowInsecureTls)
                {
                    handler.ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                }

                return handler;
            });

        return services;
    }
}
