using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Email.Data;

/// <summary>
/// Design-time DbContext factory for EF Core migrations.
/// </summary>
public class EmailDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EmailDbContext>
{
    /// <inheritdoc />
    public EmailDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EmailDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=dotnetcloud_email_dev;Username=postgres;Password=postgres",
            npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(3);
                npgsqlOptions.CommandTimeout(30);
            });
        return new EmailDbContext(optionsBuilder.Options, new PostgreSqlNamingStrategy());
    }
}
