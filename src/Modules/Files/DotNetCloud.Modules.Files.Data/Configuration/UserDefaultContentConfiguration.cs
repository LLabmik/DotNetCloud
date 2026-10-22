using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="UserContentProvisioning"/>.
/// </summary>
public sealed class UserContentProvisioningConfiguration : IEntityTypeConfiguration<UserContentProvisioning>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserContentProvisioning> builder)
    {
        builder.HasKey(p => p.UserId);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.TemplateFingerprint)
            .HasMaxLength(64);

        builder.Property(p => p.LastError)
            .HasMaxLength(1024);

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Worker claim query: rows that are queued or whose lease has expired.
        builder.HasIndex(p => new { p.Status, p.LeaseExpiresAtUtc })
            .HasDatabaseName("ix_user_content_provisioning_status_lease");
    }
}

/// <summary>
/// EF Core configuration for <see cref="UserDefaultContentItem"/>.
/// </summary>
public sealed class UserDefaultContentItemConfiguration : IEntityTypeConfiguration<UserDefaultContentItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserDefaultContentItem> builder)
    {
        builder.HasKey(i => new { i.UserId, i.RelativePathKey });

        builder.Property(i => i.RelativePathKey)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(i => i.RelativePath)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(i => i.ProvisionedContentHash)
            .HasMaxLength(64);

        builder.Property(i => i.Kind)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(i => i.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Mutation hooks and purge exclusion look rows up by node id.
        builder.HasIndex(i => i.FileNodeId)
            .HasDatabaseName("ix_user_default_content_items_file_node_id");
    }
}
