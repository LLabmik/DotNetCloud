using DotNetCloud.Core.Data.Configuration.Shared;
using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Organizations;

/// <summary>
/// EF Core fluent API configuration for the <see cref="TeamMember"/> entity.
/// </summary>
public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    /// <summary>
    /// Configures the <see cref="TeamMember"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        // Composite primary key
        builder.HasKey(tm => new { tm.TeamId, tm.UserId });

        // Properties
        builder.Property(tm => tm.TeamId)
            .IsRequired();

        builder.Property(tm => tm.UserId)
            .IsRequired();

        var roleIdsProp = builder.Property(tm => tm.RoleIds)
            .IsRequired()
            .HasConversion(RoleIdsConversion.Converter)
            .HasDefaultValue(new List<Guid>());

        roleIdsProp.Metadata.SetValueComparer(RoleIdsConversion.Comparer);

        builder.Property(tm => tm.JoinedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(tm => tm.UserId)
            .HasDatabaseName("IX_team_members_user_id");

        builder.HasIndex(tm => tm.JoinedAt)
            .HasDatabaseName("IX_team_members_joined_at");

        // Relationships
        builder.HasOne(tm => tm.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(tm => tm.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tm => tm.User)
            .WithMany()
            .HasForeignKey(tm => tm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Match the parent Team's soft-delete query filter
        builder.HasQueryFilter(tm => !tm.Team.IsDeleted);
    }
}
