using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class GuestPermissionConfiguration : IEntityTypeConfiguration<GuestPermission>
{
    public void Configure(EntityTypeBuilder<GuestPermission> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Permission)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(p => p.GuestUser)
            .WithMany(g => g.Permissions)
            .HasForeignKey(p => p.GuestUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.WorkItem)
            .WithMany()
            .HasForeignKey(p => p.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.GuestUserId, p.WorkItemId })
            .IsUnique()
            .HasDatabaseName("ix_guest_permissions_guest_work_item");
    }
}
