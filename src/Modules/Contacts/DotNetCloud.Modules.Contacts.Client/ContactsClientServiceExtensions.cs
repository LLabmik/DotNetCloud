using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Contacts.Client;

/// <summary>
/// Extension methods for registering the Contacts gRPC client configuration.
/// </summary>
public static class ContactsClientServiceExtensions
{
    /// <summary>
    /// Registers Contacts module client options from configuration.
    /// The gRPC client is generated from the proto linked in this project.
    /// Consumers create ContactsService.ContactsServiceClient with their own
    /// GrpcChannel using the registered options.
    /// </summary>
    public static IServiceCollection AddContactsClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ContactsClientOptions>(configuration.GetSection(ContactsClientOptions.SectionName));
        return services;
    }
}
