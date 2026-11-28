using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="CommentReaction"/>.
/// Uses a composite primary key of CommentId + UserId + Emoji
/// to ensure unique reactions per user per comment.
/// </summary>
public sealed class CommentReactionConfiguration : IEntityTypeConfiguration<CommentReaction>
{
    public void Configure(EntityTypeBuilder<CommentReaction> builder)
    {
        builder.HasKey(r => new { r.CommentId, r.UserId, r.Emoji });

        builder.Property(r => r.Emoji)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(r => r.Comment)
            .WithMany()
            .HasForeignKey(r => r.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.CommentId)
            .HasDatabaseName("ix_comment_reactions_comment");
    }
}
