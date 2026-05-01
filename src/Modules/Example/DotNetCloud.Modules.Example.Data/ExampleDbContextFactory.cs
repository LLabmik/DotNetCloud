using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Example.Data;

/// <summary>
/// Design-time factory for <see cref="ExampleDbContext"/> used by EF Core CLI tools
/// (e.g., <c>dotnet ef migrations add</c>). Creates a DbContext configured for
/// the PostgreSQL provider with a connection string from the environment or config.
/// </summary>
public class ExampleDbContextFactory : IDesignTimeDbContextFactory<ExampleDbContext>
{
    public ExampleDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONNECTION_STRING")
            ?? "Host=localhost;Database=dotnetcloud;Username=dotnetcloud";

        var optionsBuilder = new DbContextOptionsBuilder<ExampleDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "example"));

        return new ExampleDbContext(optionsBuilder.Options, new PostgreSqlNamingStrategy());
    }
}
