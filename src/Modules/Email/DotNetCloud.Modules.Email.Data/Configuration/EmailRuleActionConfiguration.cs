using DotNetCloud.Modules.Email.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Email.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EmailRuleAction"/> entity.
/// </summary>
public sealed class EmailRuleActionConfiguration : IEntityTypeConfiguration<EmailRuleAction>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailRuleAction> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ActionType).HasConversion<int>();
        builder.Property(a => a.TargetValue).HasMaxLength(500);
        builder.Property(a => a.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(a => a.Rule)
            .WithMany(r => r.Actions)
            .HasForeignKey(a => a.RuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.RuleId).HasDatabaseName("ix_email_rule_actions_rule_id");
    }
}
