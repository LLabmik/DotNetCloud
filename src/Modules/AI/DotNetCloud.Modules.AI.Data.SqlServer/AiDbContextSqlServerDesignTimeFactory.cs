using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.AI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.AI.Data.SqlServer;

public class AiDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    public AiDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_DB_CONNECTION")
            ?? "Server=localhost;Database=dotnetcloud_AI_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AiDbContext>();
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(AiDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });
        var namingStrategy = new SqlServerNamingStrategy();
        return new AiDbContext(options.Options, namingStrategy);
    }
}
