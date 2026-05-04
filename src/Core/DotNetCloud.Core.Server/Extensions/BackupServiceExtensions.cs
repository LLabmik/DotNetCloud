using DotNetCloud.Core.Services;
using DotNetCloud.Core.Server.Services;

namespace DotNetCloud.Core.Server.Extensions;

/// <summary>
/// Extension methods for registering backup services in the DI container.
/// </summary>
public static class BackupServiceExtensions
{
    /// <summary>
    /// Adds the <see cref="IBackupService"/> implementation and the <see cref="BackupHostedService"/> to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDotNetCloudBackupServices(this IServiceCollection services)
    {
        services.AddScoped<IBackupService, BackupService>();
        services.AddHostedService<BackupHostedService>();
        return services;
    }
}
