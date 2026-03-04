using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Channel"/> entity.
/// </summary>
public sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(c => c.Topic)
            .HasMaxLength(500);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft-delete query filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Indexes
        builder.HasIndex(c => c.OrganizationId)
            .HasDatabaseName("ix_chat_channels_organization_id");

        builder.HasIndex(c => new { c.OrganizationId, c.Name })
            .IsUnique()
            .HasDatabaseName("ix_chat_channels_org_name_unique")
            .HasFilter(null);

        builder.HasIndex(c => c.Type)
            .HasDatabaseName("ix_chat_channels_type");

        builder.HasIndex(c => c.CreatedByUserId)
            .HasDatabaseName("ix_chat_channels_created_by");

        builder.HasIndex(c => c.IsDeleted)
            .HasDatabaseName("ix_chat_channels_is_deleted");

        builder.HasIndex(c => c.LastActivityAt)
            .HasDatabaseName("ix_chat_channels_last_activity");
    }
}
