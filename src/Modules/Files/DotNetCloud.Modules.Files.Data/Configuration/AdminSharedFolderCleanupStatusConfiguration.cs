using DotNetCloud.Core.Models;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="AdminSharedFolderCleanupStatus"/>.
/// </summary>
public sealed class AdminSharedFolderCleanupStatusConfiguration : IEntityTypeConfiguration<AdminSharedFolderCleanupStatus>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AdminSharedFolderCleanupStatus> builder)
    {
        builder.ToTable("AdminSharedFolderCleanupStatuses");

        builder.HasKey(status => status.CleanupJobId);

        builder.Property(status => status.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(status => status.Phase)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(status => status.StartedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(status => status.CompletedAt);

        builder.Property(status => status.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(status => status.SearchDocsRemoved)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(status => status.SearchDocsTotal)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(status => status.AffectedUsers)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(status => status.UsersCleaned)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(status => status.MediaEntitiesRemoved)
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasIndex(status => status.SharedFolderId)
            .HasDatabaseName("ix_admin_shared_folder_cleanup_statuses_shared_folder_id");

        builder.HasIndex(status => status.StartedAt)
            .HasDatabaseName("ix_admin_shared_folder_cleanup_statuses_started_at");
    }
}
