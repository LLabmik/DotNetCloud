using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class RecurringRuleConfiguration : IEntityTypeConfiguration<RecurringRule>
{
    public void Configure(EntityTypeBuilder<RecurringRule> builder)
    {
        builder.HasKey(rr => rr.Id);

        builder.Property(rr => rr.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(rr => rr.TemplateJson)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(rr => rr.CronExpression)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(rr => rr.NextRunAt)
            .IsRequired();

        builder.Property(rr => rr.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(rr => rr.LastRunAt);

        builder.Property(rr => rr.CreatedByUserId)
            .IsRequired();

        builder.Property(rr => rr.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(rr => rr.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(rr => rr.Product)
            .WithMany(p => p.RecurringRules)
            .HasForeignKey(rr => rr.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rr => rr.Swimlane)
            .WithMany()
            .HasForeignKey(rr => rr.SwimlaneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(rr => rr.NextRunAt)
            .HasDatabaseName("ix_recurring_rules_next_run");
    }
}
