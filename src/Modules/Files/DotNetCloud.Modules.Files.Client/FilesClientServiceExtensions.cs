using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Files.Client;

/// <summary>
/// Extension methods for registering the Files gRPC client configuration.
/// </summary>
public static class FilesClientServiceExtensions
{
    /// <summary>
    /// Registers Files module client options from configuration.
    /// The gRPC client is generated from the proto linked in this project.
    /// Consumers create FilesService.FilesServiceClient with their own
    /// GrpcChannel using the registered options.
    /// </summary>
    public static IServiceCollection AddFilesClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FilesClientOptions>(configuration.GetSection(FilesClientOptions.SectionName));
        return services;
    }
}
