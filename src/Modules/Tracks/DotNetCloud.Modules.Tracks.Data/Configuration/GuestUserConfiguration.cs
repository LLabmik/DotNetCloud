using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class GuestUserConfiguration : IEntityTypeConfiguration<GuestUser>
{
    public void Configure(EntityTypeBuilder<GuestUser> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(g => g.DisplayName)
            .HasMaxLength(256);

        builder.Property(g => g.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(g => g.InviteToken)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(g => g.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(g => g.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(g => g.Product)
            .WithMany()
            .HasForeignKey(g => g.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Permissions)
            .WithOne(p => p.GuestUser)
            .HasForeignKey(p => p.GuestUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.Email)
            .HasDatabaseName("ix_guest_users_email");

        builder.HasIndex(g => g.InviteToken)
            .IsUnique()
            .HasDatabaseName("ix_guest_users_invite_token");

        builder.HasIndex(g => g.ProductId)
            .HasDatabaseName("ix_guest_users_product");
    }
}
