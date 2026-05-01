using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Notes.Data.Configuration;
using DotNetCloud.Modules.Notes.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Notes.Data;

/// <summary>
/// Entity Framework Core DbContext for the Notes module.
/// </summary>
public class NotesDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotesDbContext"/> class.
    /// </summary>
    public NotesDbContext(DbContextOptions<NotesDbContext> options)
        : this(options, new PostgreSqlNamingStrategy())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotesDbContext"/> class with a specific naming strategy.
    /// </summary>
    public NotesDbContext(DbContextOptions<NotesDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>Notes.</summary>
    public DbSet<Note> Notes => Set<Note>();

    /// <summary>Note folders.</summary>
    public DbSet<NoteFolder> NoteFolders => Set<NoteFolder>();

    /// <summary>Note tags.</summary>
    public DbSet<NoteTag> NoteTags => Set<NoteTag>();

    /// <summary>Note links to other entities.</summary>
    public DbSet<NoteLink> NoteLinks => Set<NoteLink>();

    /// <summary>Note version history.</summary>
    public DbSet<NoteVersion> NoteVersions => Set<NoteVersion>();

    /// <summary>Note shares.</summary>
    public DbSet<NoteShare> NoteShares => Set<NoteShare>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("notes"));
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new NoteConfiguration());
        modelBuilder.ApplyConfiguration(new NoteFolderConfiguration());
        modelBuilder.ApplyConfiguration(new NoteTagConfiguration());
        modelBuilder.ApplyConfiguration(new NoteLinkConfiguration());
        modelBuilder.ApplyConfiguration(new NoteVersionConfiguration());
        modelBuilder.ApplyConfiguration(new NoteShareConfiguration());
    }
}
