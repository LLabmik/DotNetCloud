using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Photos.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="PhotoEditRecord"/> entity.
/// </summary>
public sealed class PhotoEditRecordConfiguration : IEntityTypeConfiguration<PhotoEditRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PhotoEditRecord> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.OperationType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.ParametersJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(e => e.Photo)
            .WithMany(p => p.EditRecords)
            .HasForeignKey(e => e.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for retrieving edit stack in order
        builder.HasIndex(e => new { e.PhotoId, e.StackOrder })
            .HasDatabaseName("ix_photo_edit_records_photo_order");
    }
}
