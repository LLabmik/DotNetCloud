using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using FileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="FileShare"/> entity.
/// </summary>
public sealed class FileShareConfiguration : IEntityTypeConfiguration<FileShare>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileShare> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ShareType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.Permission)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.LinkToken)
            .HasMaxLength(64);

        builder.Property(s => s.LinkPasswordHash)
            .HasMaxLength(256);

        builder.Property(s => s.Note)
            .HasMaxLength(500);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(s => s.FileNode)
            .WithMany(n => n.Shares)
            .HasForeignKey(s => s.FileNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => s.FileNodeId)
            .HasDatabaseName("ix_file_shares_file_node_id");

        builder.HasIndex(s => s.SharedWithUserId)
            .HasDatabaseName("ix_file_shares_shared_with_user");

        builder.HasIndex(s => s.SharedWithTeamId)
            .HasDatabaseName("ix_file_shares_shared_with_team");

        builder.HasIndex(s => s.LinkToken)
            .IsUnique()
            .HasFilter("[LinkToken] IS NOT NULL")
            .HasDatabaseName("ix_file_shares_link_token");

        builder.HasIndex(s => s.CreatedByUserId)
            .HasDatabaseName("ix_file_shares_created_by");

        builder.HasIndex(s => s.ExpiresAt)
            .HasDatabaseName("ix_file_shares_expires_at");
    }
}
