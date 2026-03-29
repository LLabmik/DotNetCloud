using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CardAttachment"/> entity.
/// </summary>
public sealed class CardAttachmentConfiguration : IEntityTypeConfiguration<CardAttachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CardAttachment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Url)
            .HasMaxLength(2000);

        builder.Property(a => a.MimeType)
            .HasMaxLength(255);

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(a => a.Card)
            .WithMany(c => c.Attachments)
            .HasForeignKey(a => a.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(a => a.CardId)
            .HasDatabaseName("ix_card_attachments_card_id");

        builder.HasIndex(a => a.FileNodeId)
            .HasDatabaseName("ix_card_attachments_file_node_id")
            .HasFilter("\"FileNodeId\" IS NOT NULL");
    }
}
