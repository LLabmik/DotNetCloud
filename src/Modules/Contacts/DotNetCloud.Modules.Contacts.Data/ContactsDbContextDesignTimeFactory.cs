using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Contacts.Data;

/// <summary>
/// Design-time factory for creating <see cref="ContactsDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// Uses PostgreSQL as the default provider for migration generation.
/// </summary>
/// <remarks>
/// <para>
/// To generate a PostgreSQL migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Contacts/DotNetCloud.Modules.Contacts.Data
/// </code>
/// </para>
/// <para>
/// To generate a SQL Server migration, set the environment variable before running:
/// <code>
/// $env:CONTACTS_DB_PROVIDER = "SqlServer"
/// dotnet ef migrations add MigrationName_SqlServer --project src/Modules/Contacts/DotNetCloud.Modules.Contacts.Data --output-dir Migrations/SqlServer
/// </code>
/// </para>
/// </remarks>
public class ContactsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ContactsDbContext>
{
    /// <inheritdoc />
    public ContactsDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("CONTACTS_DB_PROVIDER") ?? "PostgreSQL";
        var options = new DbContextOptionsBuilder<ContactsDbContext>();

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            const string connectionString = "Server=localhost;Database=dotnetcloud_contacts_dev;Trusted_Connection=True;TrustServerCertificate=True";
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                sqlOptions.CommandTimeout(30);
            });
        }
        else
        {
            const string connectionString = "Host=localhost;Database=dotnetcloud_contacts_dev;Username=postgres;Password=postgres";
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                npgsqlOptions.CommandTimeout(30);
            });
        }

        return new ContactsDbContext(options.Options);
    }
}
