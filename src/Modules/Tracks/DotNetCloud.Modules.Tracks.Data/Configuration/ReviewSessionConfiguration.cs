using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ReviewSession"/> entity.
/// </summary>
public sealed class ReviewSessionConfiguration : IEntityTypeConfiguration<ReviewSession>
{
    /// <inheritdoc />
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

        builder.HasOne(rs => rs.Board)
            .WithMany(b => b.ReviewSessions)
            .HasForeignKey(rs => rs.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rs => rs.CurrentCard)
            .WithMany()
            .HasForeignKey(rs => rs.CurrentCardId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(rs => new { rs.BoardId, rs.Status })
            .HasDatabaseName("ix_review_sessions_board_status");

        builder.HasIndex(rs => rs.HostUserId)
            .HasDatabaseName("ix_review_sessions_host_user_id");
    }
}
