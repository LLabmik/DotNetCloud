using DotNetCloud.Modules.Email.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Email.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EmailRule"/> entity.
/// </summary>
public sealed class EmailRuleConfiguration : IEntityTypeConfiguration<EmailRule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(r => r.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(r => r.OwnerId).HasDatabaseName("ix_email_rules_owner_id");
        builder.HasIndex(r => new { r.OwnerId, r.AccountId }).HasDatabaseName("ix_email_rules_owner_account");
    }
}
