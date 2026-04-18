using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="PokerSession"/> entity.
/// </summary>
public sealed class PokerSessionConfiguration : IEntityTypeConfiguration<PokerSession>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PokerSession> builder)
    {
        builder.HasKey(ps => ps.Id);

        builder.Property(ps => ps.Scale)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(ps => ps.CustomScaleValues)
            .HasColumnType("text");

        builder.Property(ps => ps.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(ps => ps.AcceptedEstimate)
            .HasMaxLength(50);

        builder.Property(ps => ps.Round)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(ps => ps.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(ps => ps.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(ps => ps.Card)
            .WithMany(c => c.PokerSessions)
            .HasForeignKey(ps => ps.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ps => ps.Board)
            .WithMany(b => b.PokerSessions)
            .HasForeignKey(ps => ps.BoardId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(ps => ps.ReviewSession)
            .WithMany(rs => rs.PokerSessions)
            .HasForeignKey(ps => ps.ReviewSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(ps => new { ps.CardId, ps.Status })
            .HasDatabaseName("ix_poker_sessions_card_status");

        builder.HasIndex(ps => new { ps.BoardId, ps.Status })
            .HasDatabaseName("ix_poker_sessions_board_status");

        builder.HasIndex(ps => ps.CreatedByUserId)
            .HasDatabaseName("ix_poker_sessions_created_by");

        builder.HasIndex(ps => ps.ReviewSessionId)
            .HasDatabaseName("ix_poker_sessions_review_session")
            .HasFilter("\"ReviewSessionId\" IS NOT NULL");
    }
}
