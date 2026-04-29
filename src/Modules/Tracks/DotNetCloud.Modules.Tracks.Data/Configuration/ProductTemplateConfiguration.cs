using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class ProductTemplateConfiguration : IEntityTypeConfiguration<ProductTemplate>
{
    public void Configure(EntityTypeBuilder<ProductTemplate> builder)
    {
        builder.HasKey(pt => pt.Id);

        builder.Property(pt => pt.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(pt => pt.Description)
            .HasColumnType("text");

        builder.Property(pt => pt.Category)
            .HasMaxLength(100);

        builder.Property(pt => pt.DefinitionJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(pt => pt.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(pt => pt.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(pt => pt.Category)
            .HasDatabaseName("ix_product_templates_category");

        builder.HasIndex(pt => pt.IsBuiltIn)
            .HasDatabaseName("ix_product_templates_is_built_in");
    }
}
