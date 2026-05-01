using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Notes.Data;

/// <summary>
/// Design-time factory for <see cref="NotesDbContext"/> used by EF Core CLI tools.
/// </summary>
public class NotesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotesDbContext>
{
    /// <inheritdoc />
    public NotesDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Host=localhost;Database=dotnetcloud_notes_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<NotesDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        var namingStrategy = new PostgreSqlNamingStrategy();
        return new NotesDbContext(options.Options, namingStrategy);
    }
}
