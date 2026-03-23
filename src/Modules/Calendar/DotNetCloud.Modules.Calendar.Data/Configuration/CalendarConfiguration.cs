using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Calendar.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Models.Calendar"/> entity.
/// </summary>
public sealed class CalendarConfiguration : IEntityTypeConfiguration<Models.Calendar>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Models.Calendar> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.Color).HasMaxLength(20);
        builder.Property(c => c.Timezone).IsRequired().HasMaxLength(100);

        builder.Property(c => c.SyncToken)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft-delete query filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Indexes
        builder.HasIndex(c => c.OwnerId)
            .HasDatabaseName("ix_calendars_owner_id");

        builder.HasIndex(c => new { c.OwnerId, c.Name })
            .HasDatabaseName("ix_calendars_owner_name");

        builder.HasIndex(c => c.IsDeleted)
            .HasDatabaseName("ix_calendars_is_deleted");
    }
}
