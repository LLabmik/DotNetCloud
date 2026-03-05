using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

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
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>())
            .HasDefaultValue(new List<Guid>());

        roleIdsProp.Metadata.SetValueComparer(new ValueComparer<ICollection<Guid>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => (ICollection<Guid>)c.ToList()));

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
