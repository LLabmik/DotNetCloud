using DotNetCloud.Modules.Notes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Notes.Data.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="Note"/>.
/// </summary>
public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.Content)
            .HasColumnType("text");

        builder.Property(n => n.Format)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(n => n.ETag)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(n => n.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(n => n.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(n => n.CreatedByUserId);
        builder.Property(n => n.UpdatedByUserId);

        // Soft-delete query filter
        builder.HasQueryFilter(n => !n.IsDeleted);

        // Relationships
        builder.HasOne(n => n.Folder)
            .WithMany(f => f.Notes)
            .HasForeignKey(n => n.FolderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(n => n.Tags)
            .WithOne(t => t.Note)
            .HasForeignKey(t => t.NoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(n => n.Links)
            .WithOne(l => l.Note)
            .HasForeignKey(l => l.NoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(n => n.Versions)
            .WithOne(v => v.Note)
            .HasForeignKey(v => v.NoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(n => n.Shares)
            .WithOne(s => s.Note)
            .HasForeignKey(s => s.NoteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(n => n.OwnerId)
            .HasDatabaseName("ix_notes_owner_id");

        builder.HasIndex(n => new { n.OwnerId, n.FolderId })
            .HasDatabaseName("ix_notes_owner_folder");

        builder.HasIndex(n => n.IsDeleted)
            .HasDatabaseName("ix_notes_is_deleted");

        builder.HasIndex(n => n.IsPinned)
            .HasDatabaseName("ix_notes_is_pinned");
    }
}
