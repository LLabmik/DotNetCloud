using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class ItemTemplateConfiguration : IEntityTypeConfiguration<ItemTemplate>
{
    public void Configure(EntityTypeBuilder<ItemTemplate> builder)
    {
        builder.HasKey(it => it.Id);

        builder.Property(it => it.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(it => it.TitlePattern)
            .HasMaxLength(500);

        builder.Property(it => it.Description)
            .HasColumnType("text");

        builder.Property(it => it.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(it => it.LabelIdsJson)
            .HasColumnType("text");

        builder.Property(it => it.ChecklistsJson)
            .HasColumnType("text");

        builder.Property(it => it.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(it => it.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(it => it.Product)
            .WithMany()
            .HasForeignKey(it => it.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(it => it.ProductId)
            .HasDatabaseName("ix_item_templates_product_id");
    }
}
