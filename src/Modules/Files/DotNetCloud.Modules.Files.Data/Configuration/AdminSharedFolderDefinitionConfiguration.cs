using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="AdminSharedFolderDefinition"/>.
/// </summary>
public sealed class AdminSharedFolderDefinitionConfiguration : IEntityTypeConfiguration<AdminSharedFolderDefinition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AdminSharedFolderDefinition> builder)
    {
        builder.HasKey(folder => folder.Id);

        builder.Property(folder => folder.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(folder => folder.SourcePath)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(folder => folder.AccessMode)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(folder => folder.CrawlMode)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(folder => folder.LastScanStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(folder => folder.ReindexState)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(folder => folder.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(folder => folder.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasMany(folder => folder.Grants)
            .WithOne(grant => grant.AdminSharedFolder)
            .HasForeignKey(grant => grant.AdminSharedFolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(folder => folder.SourcePath)
            .IsUnique()
            .HasDatabaseName("ix_admin_shared_folders_source_path");

        builder.HasIndex(folder => new { folder.OrganizationId, folder.DisplayName })
            .IsUnique()
            .HasDatabaseName("ix_admin_shared_folders_org_display_name");

        builder.HasIndex(folder => folder.NextScheduledScanAt)
            .HasDatabaseName("ix_admin_shared_folders_next_scan");
    }
}