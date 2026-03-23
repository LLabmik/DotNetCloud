using DotNetCloud.Core.Data.Entities.Notifications;
using DotNetCloud.Core.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Notifications;

/// <summary>
/// EF Core configuration for the <see cref="Notification"/> entity.
/// </summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.SourceModuleId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(n => n.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.Message).HasMaxLength(2000);

        builder.Property(n => n.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(n => n.ActionUrl).HasMaxLength(1000);

        builder.Property(n => n.RelatedEntityType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(n => n.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(n => n.UserId)
            .HasDatabaseName("ix_notifications_user_id");

        builder.HasIndex(n => new { n.UserId, n.ReadAtUtc })
            .HasDatabaseName("ix_notifications_user_unread");

        builder.HasIndex(n => n.CreatedAtUtc)
            .HasDatabaseName("ix_notifications_created_at");
    }
}
