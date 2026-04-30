using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class CustomFieldConfiguration : IEntityTypeConfiguration<CustomField>
{
    public void Configure(EntityTypeBuilder<CustomField> builder)
    {
        builder.HasKey(cf => cf.Id);

        builder.Property(cf => cf.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(cf => cf.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(cf => cf.OptionsJson)
            .HasMaxLength(4000);

        builder.Property(cf => cf.IsRequired)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(cf => cf.Position)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(cf => cf.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(cf => cf.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(cf => cf.Product)
            .WithMany(p => p.CustomFields)
            .HasForeignKey(cf => cf.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cf => new { cf.ProductId, cf.Name })
            .IsUnique()
            .HasDatabaseName("uq_custom_fields_product_name");
    }
}
