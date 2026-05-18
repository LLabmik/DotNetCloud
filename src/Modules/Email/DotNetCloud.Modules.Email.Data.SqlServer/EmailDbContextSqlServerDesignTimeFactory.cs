using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Email.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Email.Data.SqlServer;

public class EmailDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<EmailDbContext>
{
    public EmailDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_DB_CONNECTION")
            ?? "Server=localhost;Database=dotnetcloud_Email_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<EmailDbContext>();
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(EmailDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });
        var namingStrategy = new SqlServerNamingStrategy();
        return new EmailDbContext(options.Options, namingStrategy);
    }
}
