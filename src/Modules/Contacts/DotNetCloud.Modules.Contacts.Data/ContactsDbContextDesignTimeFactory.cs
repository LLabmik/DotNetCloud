using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Contacts.Data;

/// <summary>
/// Design-time factory for creating <see cref="ContactsDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// </summary>
/// <remarks>
/// To generate a migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Contacts/DotNetCloud.Modules.Contacts.Data
/// </code>
/// </remarks>
public class ContactsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ContactsDbContext>
{
    /// <inheritdoc />
    public ContactsDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Host=localhost;Database=dotnetcloud_contacts_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<ContactsDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        return new ContactsDbContext(options.Options);
    }
}
