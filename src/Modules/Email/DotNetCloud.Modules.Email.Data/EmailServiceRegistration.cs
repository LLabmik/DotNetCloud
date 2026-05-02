using DotNetCloud.Modules.Email.Data.Services;
using DotNetCloud.Modules.Email.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        return services;
    }
}
