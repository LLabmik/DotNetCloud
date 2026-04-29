using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasColumnType("text");

        builder.Property(p => p.Color)
            .HasMaxLength(20);

        builder.Property(p => p.ETag)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasIndex(p => p.OrganizationId)
            .HasDatabaseName("ix_products_organization_id");

        builder.HasIndex(p => p.OwnerId)
            .HasDatabaseName("ix_products_owner_id");

        builder.HasIndex(p => p.IsArchived)
            .HasDatabaseName("ix_products_is_archived");

        builder.HasIndex(p => p.IsDeleted)
            .HasDatabaseName("ix_products_is_deleted");

        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("ix_products_created_at");
    }
}
