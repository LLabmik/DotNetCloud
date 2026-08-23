using DotNetCloud.Core.Data.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Audit;

/// <summary>
/// EF Core fluent API configuration for the <see cref="AuditLog"/> entity (SOC 2 CC4).
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <c>SystemSettingConfiguration</c>: a Guid primary key, required property
/// constraints with maximum lengths, snake_case column names, and an explicit
/// table name. The <c>ITableNamingStrategy</c> pass in <c>CoreDbContext.OnModelCreating</c>
/// maps this table into the <c>core</c> schema for both PostgreSQL and SQL Server.
/// </para>
/// <para>
/// Indexes are added for the retention query (<c>TimestampUtc</c>), per-module
/// monitoring (<c>ModuleId + TimestampUtc</c>), entity lookup, and caller attribution.
/// </para>
/// </remarks>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        // Properties
        builder.Property(a => a.TimestampUtc)
            .IsRequired()
            .HasColumnName("timestamp_utc");

        builder.Property(a => a.CallerType)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("caller_type");

        builder.Property(a => a.CallerUserId)
            .HasColumnName("caller_user_id");

        builder.Property(a => a.CallerRoles)
            .HasMaxLength(2000)
            .HasColumnName("caller_roles");

        builder.Property(a => a.ModuleId)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("module_id");

        builder.Property(a => a.Action)
            .IsRequired()
            .HasColumnName("action");

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("entity_type");

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasColumnName("entity_id");

        builder.Property(a => a.Description)
            .HasMaxLength(2000)
            .HasColumnName("description");

        // Indexes for retention purge, monitoring, entity lookup, and attribution
        builder.HasIndex(a => a.TimestampUtc)
            .HasDatabaseName("IX_audit_logs_timestamp_utc");

        builder.HasIndex(a => new { a.ModuleId, a.TimestampUtc })
            .HasDatabaseName("IX_audit_logs_module_timestamp");

        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("IX_audit_logs_entity");

        builder.HasIndex(a => a.CallerUserId)
            .HasDatabaseName("IX_audit_logs_caller_user");

        // Table naming is applied by ITableNamingStrategy during context configuration
        builder.ToTable("AuditLogs");
    }
}
