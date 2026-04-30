using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="Models.AutomationRule"/>.
/// </summary>
public sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<Models.AutomationRule>
{
    public void Configure(EntityTypeBuilder<Models.AutomationRule> builder)
    {
        builder.HasKey(ar => ar.Id);

        builder.Property(ar => ar.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(ar => ar.Trigger)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ar => ar.ConditionsJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(ar => ar.ActionsJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(ar => ar.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(ar => ar.Product)
            .WithMany()
            .HasForeignKey(ar => ar.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ar => ar.ProductId)
            .HasDatabaseName("ix_automation_rules_product_id");
    }
}
