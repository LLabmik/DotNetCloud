using DotNetCloud.Core.Data.Entities.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Organizations;

/// <summary>
/// EF Core fluent API configuration for the <see cref="GroupMember"/> entity.
/// </summary>
public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    /// <summary>
    /// Configures the <see cref="GroupMember"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        // Composite primary key
        builder.HasKey(gm => new { gm.GroupId, gm.UserId });

        // Properties
        builder.Property(gm => gm.GroupId)
            .IsRequired();

        builder.Property(gm => gm.UserId)
            .IsRequired();

        builder.Property(gm => gm.AddedAt)
            .IsRequired();

        builder.Property(gm => gm.AddedByUserId);

        // Indexes
        builder.HasIndex(gm => gm.UserId)
            .HasDatabaseName("IX_group_members_user_id");

        builder.HasIndex(gm => gm.AddedAt)
            .HasDatabaseName("IX_group_members_added_at");

        builder.HasIndex(gm => gm.AddedByUserId)
            .HasDatabaseName("IX_group_members_added_by");

        // Relationships
        builder.HasOne(gm => gm.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gm => gm.User)
            .WithMany()
            .HasForeignKey(gm => gm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(gm => gm.AddedByUser)
            .WithMany()
            .HasForeignKey(gm => gm.AddedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
