using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Organizations;

/// <summary>
/// EF Core fluent API configuration for the <see cref="Team"/> entity.
/// </summary>
public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    /// <summary>
    /// Configures the <see cref="Team"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        // Primary key
        builder.HasKey(t => t.Id);

        // Properties
        builder.Property(t => t.OrganizationId)
            .IsRequired();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.DeletedAt);

        // Indexes
        builder.HasIndex(t => new { t.OrganizationId, t.Name })
            .IsUnique()
            .HasDatabaseName("IX_teams_org_name");

        builder.HasIndex(t => t.IsDeleted)
            .HasDatabaseName("IX_teams_is_deleted");

        builder.HasIndex(t => t.CreatedAt)
            .HasDatabaseName("IX_teams_created_at");

        // Soft-delete query filter
        builder.HasQueryFilter(t => !t.IsDeleted);

        // Relationships
        builder.HasOne(t => t.Organization)
            .WithMany(o => o.Teams)
            .HasForeignKey(t => t.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Members)
            .WithOne(tm => tm.Team)
            .HasForeignKey(tm => tm.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
