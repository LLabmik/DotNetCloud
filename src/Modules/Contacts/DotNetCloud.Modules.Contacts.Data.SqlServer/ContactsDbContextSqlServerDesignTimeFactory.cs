using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Contacts.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Contacts.Data.SqlServer;

/// <summary>
/// Design-time factory for generating SQL Server migrations for ContactsDbContext.
/// </summary>
/// <remarks>
/// To generate a SQL Server migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Contacts/DotNetCloud.Modules.Contacts.Data.SqlServer --startup-project src/Modules/Contacts/DotNetCloud.Modules.Contacts.Data.SqlServer
/// </code>
/// </remarks>
public class ContactsDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<ContactsDbContext>
{
    /// <inheritdoc />
    public ContactsDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Server=localhost;Database=dotnetcloud_contacts_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<ContactsDbContext>();

        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(ContactsDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });

        var namingStrategy = new SqlServerNamingStrategy();
        return new ContactsDbContext(options.Options, namingStrategy);
    }
}
