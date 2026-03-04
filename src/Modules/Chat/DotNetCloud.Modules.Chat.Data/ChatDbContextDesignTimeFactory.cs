using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Chat.Data;

/// <summary>
/// Design-time factory for creating <see cref="ChatDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// Uses PostgreSQL as the default provider for migration generation.
/// </summary>
/// <remarks>
/// <para>
/// To generate a PostgreSQL migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Chat/DotNetCloud.Modules.Chat.Data
/// </code>
/// </para>
/// <para>
/// To generate a SQL Server migration, set the environment variable before running:
/// <code>
/// $env:CHAT_DB_PROVIDER = "SqlServer"
/// dotnet ef migrations add MigrationName_SqlServer --project src/Modules/Chat/DotNetCloud.Modules.Chat.Data --output-dir Migrations/SqlServer
/// </code>
/// </para>
/// </remarks>
public class ChatDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ChatDbContext>
{
    /// <inheritdoc />
    public ChatDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("CHAT_DB_PROVIDER") ?? "PostgreSQL";
        var options = new DbContextOptionsBuilder<ChatDbContext>();

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            const string connectionString = "Server=localhost;Database=dotnetcloud_chat_dev;Trusted_Connection=True;TrustServerCertificate=True";
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                sqlOptions.CommandTimeout(30);
            });
        }
        else
        {
            const string connectionString = "Host=localhost;Database=dotnetcloud_chat_dev;Username=postgres;Password=postgres";
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                npgsqlOptions.CommandTimeout(30);
            });
        }

        return new ChatDbContext(options.Options);
    }
}
