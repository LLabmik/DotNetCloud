using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="SyncFolderRegistration"/> entity.
/// </summary>
public sealed class SyncFolderRegistrationConfiguration : IEntityTypeConfiguration<SyncFolderRegistration>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SyncFolderRegistration> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RemoteFolderPath)
            .HasMaxLength(4000);

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(r => r.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("ix_sync_folder_registrations_user_id");

        builder.HasIndex(r => new { r.UserId, r.RemoteFolderNodeId })
            .IsUnique()
            .HasDatabaseName("uq_sync_folder_registrations_user_folder");
    }
}
