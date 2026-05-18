using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Bookmarks.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Bookmarks.Data.SqlServer;

public class BookmarksDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<BookmarksDbContext>
{
    public BookmarksDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_DB_CONNECTION")
            ?? "Server=localhost;Database=dotnetcloud_Bookmarks_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<BookmarksDbContext>();
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(BookmarksDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });
        var namingStrategy = new SqlServerNamingStrategy();
        return new BookmarksDbContext(options.Options, namingStrategy);
    }
}
