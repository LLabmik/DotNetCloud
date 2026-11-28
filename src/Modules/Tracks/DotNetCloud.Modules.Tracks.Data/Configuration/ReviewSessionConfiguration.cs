using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class ReviewSessionConfiguration : IEntityTypeConfiguration<ReviewSession>
{
    public void Configure(EntityTypeBuilder<ReviewSession> builder)
    {
        builder.HasKey(rs => rs.Id);

        builder.Property(rs => rs.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ReviewSessionStatus.Active);

        builder.Property(rs => rs.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(rs => rs.Epic)
            .WithMany()
            .HasForeignKey(rs => rs.EpicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rs => rs.CurrentItem)
            .WithMany()
            .HasForeignKey(rs => rs.CurrentItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(rs => new { rs.EpicId, rs.Status })
            .HasDatabaseName("ix_review_sessions_epic_status");

        builder.HasIndex(rs => rs.HostUserId)
            .HasDatabaseName("ix_review_sessions_host_user_id");
    }
}
