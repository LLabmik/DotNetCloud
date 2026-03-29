using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="BoardMember"/> entity.
/// </summary>
public sealed class BoardMemberConfiguration : IEntityTypeConfiguration<BoardMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BoardMember> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.JoinedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(m => m.Board)
            .WithMany(b => b.Members)
            .HasForeignKey(m => m.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: one membership per user per board
        builder.HasIndex(m => new { m.BoardId, m.UserId })
            .IsUnique()
            .HasDatabaseName("uq_board_members_board_user");

        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("ix_board_members_user_id");
    }
}
