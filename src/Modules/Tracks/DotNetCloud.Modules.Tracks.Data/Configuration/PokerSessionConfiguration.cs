using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class PokerSessionConfiguration : IEntityTypeConfiguration<PokerSession>
{
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

        builder.HasOne(ps => ps.Item)
            .WithMany(wi => wi.PokerSessions)
            .HasForeignKey(ps => ps.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ps => ps.Epic)
            .WithMany()
            .HasForeignKey(ps => ps.EpicId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(ps => ps.ReviewSession)
            .WithMany(rs => rs.PokerSessions)
            .HasForeignKey(ps => ps.ReviewSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(ps => new { ps.ItemId, ps.Status })
            .HasDatabaseName("ix_poker_sessions_item_status");

        builder.HasIndex(ps => new { ps.EpicId, ps.Status })
            .HasDatabaseName("ix_poker_sessions_epic_status");

        builder.HasIndex(ps => ps.CreatedByUserId)
            .HasDatabaseName("ix_poker_sessions_created_by");

        builder.HasIndex(ps => ps.ReviewSessionId)
            .HasDatabaseName("ix_poker_sessions_review_session")
            .HasFilter("\"ReviewSessionId\" IS NOT NULL");
    }
}
