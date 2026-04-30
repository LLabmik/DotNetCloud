using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="WebhookDelivery"/>.
/// </summary>
public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.PayloadJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(d => d.ResponseStatusCode);

        builder.Property(d => d.ResponseBody)
            .HasColumnType("text");

        builder.Property(d => d.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(d => d.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(d => d.Subscription!)
            .WithMany(s => s.Deliveries)
            .HasForeignKey(d => d.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.SubscriptionId)
            .HasDatabaseName("ix_webhook_deliveries_subscription");

        builder.HasIndex(d => d.CreatedAt)
            .HasDatabaseName("ix_webhook_deliveries_created");
    }
}
