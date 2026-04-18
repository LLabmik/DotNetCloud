using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ReviewSessionParticipant"/> entity.
/// </summary>
public sealed class ReviewSessionParticipantConfiguration : IEntityTypeConfiguration<ReviewSessionParticipant>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReviewSessionParticipant> builder)
    {
        builder.HasKey(rsp => rsp.Id);

        builder.Property(rsp => rsp.JoinedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(rsp => rsp.ReviewSession)
            .WithMany(rs => rs.Participants)
            .HasForeignKey(rsp => rsp.ReviewSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: one participant record per user per session
        builder.HasIndex(rsp => new { rsp.ReviewSessionId, rsp.UserId })
            .IsUnique()
            .HasDatabaseName("ix_review_session_participants_session_user");

        builder.HasIndex(rsp => rsp.UserId)
            .HasDatabaseName("ix_review_session_participants_user_id");
    }
}
