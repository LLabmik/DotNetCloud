using DotNetCloud.Core.Data.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Admin;

/// <summary>
/// EF Core configuration for the <see cref="AdminBroadcastDismissal"/> entity.
/// </summary>
public sealed class AdminBroadcastDismissalConfiguration : IEntityTypeConfiguration<AdminBroadcastDismissal>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AdminBroadcastDismissal> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.BroadcastId)
            .IsRequired();

        builder.Property(d => d.UserId)
            .IsRequired();

        builder.Property(d => d.DismissedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Deleting a broadcast removes its dismissals — they are meaningless afterwards.
        builder.HasOne<AdminBroadcast>()
            .WithMany()
            .HasForeignKey(d => d.BroadcastId)
            .OnDelete(DeleteBehavior.Cascade);

        // A user can dismiss a given broadcast at most once.
        builder.HasIndex(d => new { d.BroadcastId, d.UserId })
            .IsUnique()
            .HasDatabaseName("ix_admin_broadcast_dismissals_broadcast_user");

        builder.HasIndex(d => d.UserId)
            .HasDatabaseName("ix_admin_broadcast_dismissals_user");
    }
}
