using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Chat.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Chat.Data.SqlServer;

public class ChatDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<ChatDbContext>
{
    public ChatDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_DB_CONNECTION")
            ?? "Server=localhost;Database=dotnetcloud_Chat_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<ChatDbContext>();
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(ChatDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });
        var namingStrategy = new SqlServerNamingStrategy();
        return new ChatDbContext(options.Options, namingStrategy);
    }
}
