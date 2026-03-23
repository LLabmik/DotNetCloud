using DotNetCloud.Modules.Notes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Notes.Data.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="NoteLink"/>.
/// </summary>
public sealed class NoteLinkConfiguration : IEntityTypeConfiguration<NoteLink>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NoteLink> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.LinkType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(l => l.DisplayLabel)
            .HasMaxLength(300);

        // Index for looking up links by target
        builder.HasIndex(l => new { l.NoteId, l.TargetId })
            .HasDatabaseName("ix_note_links_note_target");

        builder.HasIndex(l => l.TargetId)
            .HasDatabaseName("ix_note_links_target_id");
    }
}
