using DotNetCloud.Modules.Email.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Email.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EmailRuleConditionGroup"/> entity.
/// </summary>
public sealed class EmailRuleConditionGroupConfiguration : IEntityTypeConfiguration<EmailRuleConditionGroup>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailRuleConditionGroup> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.MatchMode).HasConversion<int>();
        builder.Property(g => g.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(g => g.Rule)
            .WithMany(r => r.ConditionGroups)
            .HasForeignKey(g => g.RuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.RuleId).HasDatabaseName("ix_email_rule_condition_groups_rule_id");
    }
}
