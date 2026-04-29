using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class ProductMemberConfiguration : IEntityTypeConfiguration<ProductMember>
{
    public void Configure(EntityTypeBuilder<ProductMember> builder)
    {
        builder.HasKey(pm => new { pm.ProductId, pm.UserId });

        builder.Property(pm => pm.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(pm => pm.JoinedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(pm => pm.Product)
            .WithMany(p => p.Members)
            .HasForeignKey(pm => pm.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pm => pm.UserId)
            .HasDatabaseName("ix_product_members_user_id");

        builder.HasIndex(pm => new { pm.ProductId, pm.Role })
            .HasDatabaseName("ix_product_members_product_role");
    }
}
