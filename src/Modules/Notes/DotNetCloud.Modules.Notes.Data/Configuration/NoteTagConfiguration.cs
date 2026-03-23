using DotNetCloud.Modules.Notes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Notes.Data.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="NoteTag"/>.
/// </summary>
public sealed class NoteTagConfiguration : IEntityTypeConfiguration<NoteTag>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NoteTag> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Tag)
            .IsRequired()
            .HasMaxLength(100);

        // Unique constraint: one tag value per note
        builder.HasIndex(t => new { t.NoteId, t.Tag })
            .IsUnique()
            .HasDatabaseName("ix_note_tags_note_tag");

        builder.HasIndex(t => t.Tag)
            .HasDatabaseName("ix_note_tags_tag");

        builder.Property(t => t.CreatedByUserId);
    }
}
