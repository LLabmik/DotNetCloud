using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="AdminSharedFolderGrant"/>.
/// </summary>
public sealed class AdminSharedFolderGrantConfiguration : IEntityTypeConfiguration<AdminSharedFolderGrant>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AdminSharedFolderGrant> builder)
    {
        builder.HasKey(grant => grant.Id);

        builder.Property(grant => grant.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(grant => new { grant.AdminSharedFolderId, grant.GroupId })
            .IsUnique()
            .HasDatabaseName("ix_admin_shared_folder_grants_folder_group");

        builder.HasIndex(grant => grant.GroupId)
            .HasDatabaseName("ix_admin_shared_folder_grants_group_id");
    }
}