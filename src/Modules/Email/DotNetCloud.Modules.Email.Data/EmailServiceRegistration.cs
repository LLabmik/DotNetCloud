using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Email.Data.Services;
using DotNetCloud.Modules.Email.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.TryAddSingleton<IBackgroundServiceTracker, BackgroundServiceTracker>();

        services.AddScoped<IEmailAccountService, EmailAccountService>();
        services.AddScoped<IEmailSendService, EmailSendService>();
        services.AddScoped<IEmailRuleService, EmailRuleService>();
        services.AddScoped<IEmailProvider, ImapSmtpEmailProvider>();
        services.AddScoped<IEmailProvider, GmailEmailProvider>();
        services.AddSingleton<EmailCredentialEncryptionService>();
        // Syncs are core-driven: this service is not a hosted service and never schedules itself.
        // The core lists due candidates and invokes a sync naming a user it has signed for.
        services.AddSingleton<EmailSyncService>();
        services.AddSingleton<IEmailSyncService>(sp => sp.GetRequiredService<EmailSyncService>());

        // Attachment storage
        services.Configure<AttachmentStorageOptions>(configuration.GetSection("Email:AttachmentStorage"));
        services.AddScoped<IAttachmentStorage, FileSystemAttachmentStorage>();

        // Temp attachment cleanup background job
        services.AddHostedService<CleanupTempAttachmentsBackgroundService>();

        return services;
    }
}
