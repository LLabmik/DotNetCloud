using System;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Example.Data;

/// <summary>
/// Design-time factory for generating SQL Server migrations for <see cref="ExampleDbContext"/>.
/// </summary>
/// <remarks>
/// To generate a SQL Server migration:
/// <code>
/// dotnet ef migrations add MigrationName --project src/Modules/Example/DotNetCloud.Modules.Example.Data.SqlServer --context DotNetCloud.Modules.Example.Data.ExampleDbContext
/// </code>
/// </remarks>
public class ExampleDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<ExampleDbContext>
{
    /// <inheritdoc />
    public ExampleDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_DB_CONNECTION")
            ?? "Server=localhost;Database=dotnetcloud_example_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<ExampleDbContext>();

        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(ExampleDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });

        return new ExampleDbContext(options.Options, new SqlServerNamingStrategy());
    }
}
