using DotNetCloud.Modules.Notes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Notes.Data.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="NoteVersion"/>.
/// </summary>
public sealed class NoteVersionConfiguration : IEntityTypeConfiguration<NoteVersion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NoteVersion> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.Content)
            .HasColumnType("text");

        builder.Property(v => v.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one version number per note
        builder.HasIndex(v => new { v.NoteId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ix_note_versions_note_version");

        builder.HasIndex(v => v.NoteId)
            .HasDatabaseName("ix_note_versions_note_id");
    }
}
