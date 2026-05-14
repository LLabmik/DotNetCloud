using System;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.AI.Data;

/// <summary>
/// Design-time factory for <see cref="AiDbContext"/> to support EF Core migrations.
/// </summary>
public sealed class AiDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    /// <inheritdoc />
    public AiDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_DB_CONNECTION")
            ?? "Host=localhost;Database=dotnetcloud_ai;Username=dotnetcloud;Password=dev";
        var optionsBuilder = new DbContextOptionsBuilder<AiDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        var namingStrategy = new PostgreSqlNamingStrategy();
        return new AiDbContext(optionsBuilder.Options, namingStrategy);
    }
}
