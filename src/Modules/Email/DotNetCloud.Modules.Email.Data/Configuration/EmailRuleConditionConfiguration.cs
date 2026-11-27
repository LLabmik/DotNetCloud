using DotNetCloud.Modules.Email.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Email.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EmailRuleCondition"/> entity.
/// </summary>
public sealed class EmailRuleConditionConfiguration : IEntityTypeConfiguration<EmailRuleCondition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailRuleCondition> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Field).HasConversion<int>();
        builder.Property(c => c.Operator).HasConversion<int>();
        builder.Property(c => c.Value).IsRequired().HasMaxLength(500);
        builder.Property(c => c.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(c => c.ConditionGroup)
            .WithMany(g => g.Conditions)
            .HasForeignKey(c => c.ConditionGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.ConditionGroupId).HasDatabaseName("ix_email_rule_conditions_group_id");
    }
}
