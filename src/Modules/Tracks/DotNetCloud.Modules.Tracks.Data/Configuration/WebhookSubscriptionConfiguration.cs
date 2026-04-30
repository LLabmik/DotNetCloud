using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="WebhookSubscription"/>.
/// </summary>
public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(w => w.Secret)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(w => w.EventsJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(w => w.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(w => w.CreatedByUserId)
            .IsRequired();

        builder.Property(w => w.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(w => w.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(w => w.Product)
            .WithMany()
            .HasForeignKey(w => w.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => w.ProductId)
            .HasDatabaseName("ix_webhook_subscriptions_product");

        builder.HasIndex(w => w.IsActive)
            .HasDatabaseName("ix_webhook_subscriptions_active");
    }
}
