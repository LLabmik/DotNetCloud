using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Bookmarks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Bookmarks.Data;

/// <summary>
/// Entity Framework Core database context for the Bookmarks module.
/// </summary>
public class BookmarksDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>Bookmarks.</summary>
    public DbSet<BookmarkItem> Bookmarks => Set<BookmarkItem>();

    /// <summary>Bookmark folders.</summary>
    public DbSet<BookmarkFolder> BookmarkFolders => Set<BookmarkFolder>();

    /// <summary>Bookmark previews.</summary>
    public DbSet<BookmarkPreview> BookmarkPreviews => Set<BookmarkPreview>();

    /// <summary>
    /// Initializes a new instance with the default PostgreSQL naming strategy.
    /// </summary>
    public BookmarksDbContext(DbContextOptions<BookmarksDbContext> options)
        : this(options, new PostgreSqlNamingStrategy()) { }

    /// <summary>
    /// Initializes a new instance with the specified naming strategy.
    /// </summary>
    public BookmarksDbContext(DbContextOptions<BookmarksDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("bookmarks"));
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new Configuration.BookmarkFolderConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.BookmarkItemConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.BookmarkPreviewConfiguration());
    }
}
