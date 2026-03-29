using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="PokerVote"/> entity.
/// </summary>
public sealed class PokerVoteConfiguration : IEntityTypeConfiguration<PokerVote>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PokerVote> builder)
    {
        builder.HasKey(pv => pv.Id);

        builder.Property(pv => pv.Estimate)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(pv => pv.Round)
            .IsRequired();

        builder.Property(pv => pv.VotedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(pv => pv.Session)
            .WithMany(ps => ps.Votes)
            .HasForeignKey(pv => pv.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // One vote per user per round per session
        builder.HasIndex(pv => new { pv.SessionId, pv.UserId, pv.Round })
            .IsUnique()
            .HasDatabaseName("ix_poker_votes_session_user_round");

        builder.HasIndex(pv => pv.UserId)
            .HasDatabaseName("ix_poker_votes_user");
    }
}
