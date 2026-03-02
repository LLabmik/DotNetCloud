using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DotNetCloud.Core.Data.Entities.Modules;

namespace DotNetCloud.Core.Data.Configuration.Modules;

/// <summary>
/// EF Core configuration for the UserDevice entity.
/// </summary>
public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    /// <summary>
    /// Configures the UserDevice entity mapping, constraints, and indexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        // Table naming will be handled by ITableNamingStrategy
        // PostgreSQL: core.user_devices
        // SQL Server: [core].[UserDevices]
        // MariaDB: core_user_devices

        // Primary Key
        builder.HasKey(d => d.Id);

        // Properties
        builder.Property(d => d.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(d => d.UserId)
            .IsRequired();

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.DeviceType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.PushToken)
            .HasMaxLength(500);

        builder.Property(d => d.LastSeenAt)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes for efficient querying
        builder.HasIndex(d => d.UserId)
            .HasDatabaseName("ix_user_devices_user_id");

        builder.HasIndex(d => d.LastSeenAt)
            .HasDatabaseName("ix_user_devices_last_seen_at");

        builder.HasIndex(d => new { d.UserId, d.DeviceType })
            .HasDatabaseName("ix_user_devices_user_id_device_type");

        // Relationships
        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_devices_user_id");
    }
}
