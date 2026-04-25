using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="MountedNodeEntry"/>.
/// </summary>
public sealed class MountedNodeEntryConfiguration : IEntityTypeConfiguration<MountedNodeEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MountedNodeEntry> builder)
    {
        builder.ToTable("MountedNodeEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.RelativePath)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(entry => entry.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(entry => entry.SharedFolder)
            .WithMany()
            .HasForeignKey(entry => entry.SharedFolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entry => entry.SharedFolderId)
            .HasDatabaseName("ix_mounted_node_entries_shared_folder_id");
    }
}
