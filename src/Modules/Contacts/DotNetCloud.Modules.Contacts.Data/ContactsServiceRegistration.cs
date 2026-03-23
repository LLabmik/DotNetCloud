using DotNetCloud.Modules.Contacts.Data.Services;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Contacts.Data;

/// <summary>
/// Extension methods for registering contacts services in the DI container.
/// </summary>
public static class ContactsServiceRegistration
{
    /// <summary>
    /// Registers all contacts service implementations in the DI container.
    /// </summary>
    public static IServiceCollection AddContactsServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IContactGroupService, ContactGroupService>();
        services.AddScoped<IContactShareService, ContactShareService>();
        services.AddScoped<IVCardService, VCardService>();

        return services;
    }
}
