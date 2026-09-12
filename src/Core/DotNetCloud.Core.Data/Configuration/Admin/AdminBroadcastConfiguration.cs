using DotNetCloud.Core.Data.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Admin;

/// <summary>
/// EF Core configuration for the <see cref="AdminBroadcast"/> entity.
/// </summary>
public sealed class AdminBroadcastConfiguration : IEntityTypeConfiguration<AdminBroadcast>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AdminBroadcast> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Message)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(b => b.Severity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(b => b.CreatedByUserId)
            .IsRequired();

        builder.Property(b => b.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Scheduler lookup: unsent broadcasts whose scheduled time has passed.
        builder.HasIndex(b => new { b.SentAtUtc, b.ScheduledForUtc })
            .HasDatabaseName("ix_admin_broadcasts_pending");

        // Active-broadcast lookup: newest delivered broadcast first.
        builder.HasIndex(b => b.SentAtUtc)
            .HasDatabaseName("ix_admin_broadcasts_sent_at");
    }
}
