using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Files.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Files.Data.SqlServer;

public class FilesDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<FilesDbContext>
{
    public FilesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_DB_CONNECTION")
            ?? "Server=localhost;Database=dotnetcloud_Files_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<FilesDbContext>();
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(FilesDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });
        var namingStrategy = new SqlServerNamingStrategy();
        return new FilesDbContext(options.Options, namingStrategy);
    }
}
