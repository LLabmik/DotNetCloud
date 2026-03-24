using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Chat.Data;

/// <summary>
/// Design-time factory for creating <see cref="ChatDbContext"/> instances.
/// Required by EF Core tooling to generate migrations.
/// </summary>
/// <remarks>
/// To generate a migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Chat/DotNetCloud.Modules.Chat.Data
/// </code>
/// </remarks>
public class ChatDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ChatDbContext>
{
    /// <inheritdoc />
    public ChatDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Host=localhost;Database=dotnetcloud_chat_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<ChatDbContext>();

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        });

        return new ChatDbContext(options.Options);
    }
}
