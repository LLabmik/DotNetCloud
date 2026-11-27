using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class SprintDiscussionConfiguration : IEntityTypeConfiguration<SprintDiscussion>
{
    public void Configure(EntityTypeBuilder<SprintDiscussion> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.UserDisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes for chronological fetch per scope
        builder.HasIndex(x => new { x.SprintId, x.CreatedAt })
            .HasDatabaseName("ix_sprint_discussions_sprint_created");

        builder.HasIndex(x => new { x.ReviewSessionId, x.CreatedAt })
            .HasDatabaseName("ix_sprint_discussions_review_created");

        // FKs with cascade delete
        builder.HasOne(x => x.Sprint)
            .WithMany()
            .HasForeignKey(x => x.SprintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReviewSession)
            .WithMany()
            .HasForeignKey(x => x.ReviewSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // No FK on UserId — cross-module reference
    }
}
