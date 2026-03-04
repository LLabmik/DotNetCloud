using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ChunkedUploadSession"/> entity.
/// </summary>
public sealed class ChunkedUploadSessionConfiguration : IEntityTypeConfiguration<ChunkedUploadSession>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChunkedUploadSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.MimeType)
            .HasMaxLength(255);

        builder.Property(s => s.ChunkManifest)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("ix_upload_sessions_user_id");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_upload_sessions_status");

        builder.HasIndex(s => s.ExpiresAt)
            .HasDatabaseName("ix_upload_sessions_expires_at");
    }
}
