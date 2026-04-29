using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Details)
            .HasColumnType("text");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(a => a.Product)
            .WithMany(p => p.Activities)
            .HasForeignKey(a => a.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.ProductId, a.CreatedAt })
            .HasDatabaseName("ix_activities_product_created");

        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("ix_activities_entity");

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("ix_activities_user_id");
    }
}
