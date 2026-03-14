using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="FileChunk"/> entity.
/// </summary>
public sealed class FileChunkConfiguration : IEntityTypeConfiguration<FileChunk>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileChunk> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ChunkHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(c => c.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.LastReferencedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint on chunk hash for deduplication
        builder.HasIndex(c => c.ChunkHash)
            .IsUnique()
            .HasDatabaseName("ix_file_chunks_hash");

        builder.HasIndex(c => c.ReferenceCount)
            .HasDatabaseName("ix_file_chunks_ref_count");

        // Safety net: prevent garbage collection from driving refcount negative.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_file_chunks_ref_count_non_negative",
            "\"ReferenceCount\" >= 0"));
    }
}
