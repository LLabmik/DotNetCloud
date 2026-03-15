using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="SyncDeviceCursor"/> entity.
/// </summary>
public sealed class SyncDeviceCursorConfiguration : IEntityTypeConfiguration<SyncDeviceCursor>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SyncDeviceCursor> builder)
    {
        builder.HasKey(c => c.DeviceId);

        builder.Property(c => c.DeviceId)
            .ValueGeneratedNever();

        builder.Property(c => c.LastAcknowledgedSequence)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(c => c.Device)
            .WithOne()
            .HasForeignKey<SyncDeviceCursor>(c => c.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for querying cursors by user (admin dashboard, sync lag monitoring)
        builder.HasIndex(c => c.UserId)
            .HasDatabaseName("ix_sync_device_cursors_user_id");
    }
}
