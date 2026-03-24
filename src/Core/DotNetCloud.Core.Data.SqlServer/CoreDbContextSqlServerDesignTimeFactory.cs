using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Core.Data.SqlServer;

/// <summary>
/// Design-time factory for generating SQL Server migrations for CoreDbContext.
/// </summary>
/// <remarks>
/// To generate a SQL Server migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Core/DotNetCloud.Core.Data.SqlServer --startup-project src/Core/DotNetCloud.Core.Data.SqlServer
/// </code>
/// </remarks>
public class CoreDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    /// <inheritdoc />
    public CoreDbContext CreateDbContext(string[] args)
    {
        const string connectionString = "Server=localhost;Database=dotnetcloud_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var namingStrategy = new SqlServerNamingStrategy();
        var options = new DbContextOptionsBuilder<CoreDbContext>();

        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(CoreDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });

        return new CoreDbContext(options.Options, namingStrategy);
    }
}
