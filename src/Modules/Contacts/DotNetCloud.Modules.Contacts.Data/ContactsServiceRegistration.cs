using DotNetCloud.Core.Import;
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
        services.AddScoped<IImportProvider, ContactsImportProvider>();

        // Avatar/attachment storage path
        var storagePath = configuration.GetValue<string>("Contacts:StoragePath");
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            var dataDir = System.Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
            storagePath = !string.IsNullOrWhiteSpace(dataDir)
                ? System.IO.Path.Combine(dataDir, "contacts")
                : System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "storage", "contacts");
        }

        services.AddScoped<IContactAvatarService>(sp =>
            new ContactAvatarService(
                sp.GetRequiredService<ContactsDbContext>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ContactAvatarService>>(),
                storagePath));

        return services;
    }
}
