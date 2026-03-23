using DotNetCloud.Modules.Notes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Notes.Data.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="NoteFolder"/>.
/// </summary>
public sealed class NoteFolderConfiguration : IEntityTypeConfiguration<NoteFolder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NoteFolder> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.Color)
            .HasMaxLength(20);

        builder.Property(f => f.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(f => f.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft-delete query filter
        builder.HasQueryFilter(f => !f.IsDeleted);

        // Self-referencing hierarchy
        builder.HasOne(f => f.Parent)
            .WithMany(f => f.Children)
            .HasForeignKey(f => f.ParentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(f => f.OwnerId)
            .HasDatabaseName("ix_note_folders_owner_id");

        builder.HasIndex(f => new { f.OwnerId, f.ParentId })
            .HasDatabaseName("ix_note_folders_owner_parent");

        builder.HasIndex(f => new { f.OwnerId, f.Name })
            .HasDatabaseName("ix_note_folders_owner_name");
    }
}
