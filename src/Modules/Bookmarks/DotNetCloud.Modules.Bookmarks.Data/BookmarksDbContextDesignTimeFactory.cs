using System;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Bookmarks.Data;

/// <summary>
/// Design-time DbContext factory for EF Core migrations.
/// </summary>
public class BookmarksDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BookmarksDbContext>
{
    /// <inheritdoc />
    public BookmarksDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_DB_CONNECTION")
            ?? "Host=localhost;Database=dotnetcloud_bookmarks_dev;Username=postgres;Password=postgres";
        var optionsBuilder = new DbContextOptionsBuilder<BookmarksDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(3);
                npgsqlOptions.CommandTimeout(30);
            });
        return new BookmarksDbContext(optionsBuilder.Options, new PostgreSqlNamingStrategy());
    }
}
