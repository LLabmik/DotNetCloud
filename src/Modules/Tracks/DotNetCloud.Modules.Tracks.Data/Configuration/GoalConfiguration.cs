using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="Models.Goal"/>.
/// </summary>
public sealed class GoalConfiguration : IEntityTypeConfiguration<Models.Goal>
{
    public void Configure(EntityTypeBuilder<Models.Goal> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(g => g.Description)
            .HasColumnType("text");

        builder.Property(g => g.Type)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(g => g.ProgressType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(g => g.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(g => g.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(g => g.Product)
            .WithMany()
            .HasForeignKey(g => g.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(g => g.ParentGoal)
            .WithMany(g => g.ChildGoals)
            .HasForeignKey(g => g.ParentGoalId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(g => g.ProductId)
            .HasDatabaseName("ix_goals_product_id");

        builder.HasIndex(g => g.ParentGoalId)
            .HasDatabaseName("ix_goals_parent_goal_id");
    }
}

/// <summary>
/// EF Core configuration for <see cref="Models.GoalWorkItem"/>.
/// </summary>
public sealed class GoalWorkItemConfiguration : IEntityTypeConfiguration<Models.GoalWorkItem>
{
    public void Configure(EntityTypeBuilder<Models.GoalWorkItem> builder)
    {
        builder.HasKey(gwi => gwi.Id);

        builder.HasOne(gwi => gwi.Goal)
            .WithMany(g => g.LinkedWorkItems)
            .HasForeignKey(gwi => gwi.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gwi => gwi.WorkItem)
            .WithMany()
            .HasForeignKey(gwi => gwi.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(gwi => new { gwi.GoalId, gwi.WorkItemId })
            .IsUnique()
            .HasDatabaseName("uq_goal_workitem");
    }
}
