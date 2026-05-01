using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Notes.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Notes.Data.SqlServer;

/// <summary>
/// Design-time factory for generating SQL Server migrations for NotesDbContext.
/// </summary>
/// <remarks>
/// To generate a SQL Server migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Notes/DotNetCloud.Modules.Notes.Data.SqlServer --startup-project src/Modules/Notes/DotNetCloud.Modules.Notes.Data.SqlServer
/// </code>
/// </remarks>
public class NotesDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<NotesDbContext>
{
    /// <inheritdoc />
    public NotesDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Server=localhost;Database=dotnetcloud_notes_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<NotesDbContext>();

        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(NotesDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });

        var namingStrategy = new SqlServerNamingStrategy();
        return new NotesDbContext(options.Options, namingStrategy);
    }
}
