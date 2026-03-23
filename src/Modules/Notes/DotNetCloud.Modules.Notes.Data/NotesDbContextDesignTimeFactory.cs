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
        var provider = Environment.GetEnvironmentVariable("NOTES_DB_PROVIDER") ?? "PostgreSQL";
        var options = new DbContextOptionsBuilder<NotesDbContext>();

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            const string connectionString = "Server=localhost;Database=dotnetcloud_notes_dev;Trusted_Connection=True;TrustServerCertificate=True";
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                sqlOptions.CommandTimeout(30);
            });
        }
        else
        {
            const string connectionString = "Host=localhost;Database=dotnetcloud_notes_dev;Username=postgres;Password=postgres";
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                npgsqlOptions.CommandTimeout(30);
            });
        }

        return new NotesDbContext(options.Options);
    }
}
