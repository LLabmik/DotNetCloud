using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="FileVersionChunk"/> junction entity.
/// </summary>
public sealed class FileVersionChunkConfiguration : IEntityTypeConfiguration<FileVersionChunk>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileVersionChunk> builder)
    {
        builder.HasKey(vc => new { vc.FileVersionId, vc.FileChunkId, vc.SequenceIndex });

        builder.HasOne(vc => vc.FileVersion)
            .WithMany()
            .HasForeignKey(vc => vc.FileVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(vc => vc.FileChunk)
            .WithMany()
            .HasForeignKey(vc => vc.FileChunkId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index for looking up all chunks for a version in order
        builder.HasIndex(vc => new { vc.FileVersionId, vc.SequenceIndex })
            .HasDatabaseName("ix_file_version_chunks_version_seq");
    }
}
