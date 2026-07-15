using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Notes.Data.Context;

/// <summary>
/// Design-time factory for creating <see cref="NotesDbContext"/> instances.
/// Required by EF Core tooling to generate migrations. Uses PostgreSQL
/// as the database provider for migration generation.
/// </summary>
/// <remarks>
/// To generate a migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Notes/DotNetCloud.Modules.Notes.Data --context NotesDbContext
/// </code>
/// </remarks>
public class NotesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotesDbContext>
{
    /// <inheritdoc />
    public NotesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_DB_CONNECTION")
            ?? "Host=localhost;Database=dotnetcloud_dev;Username=postgres;Password=postgres";
        var namingStrategy = new PostgreSqlNamingStrategy();
        var options = new DbContextOptionsBuilder<NotesDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        return new NotesDbContext(options.Options, namingStrategy);
    }
}
