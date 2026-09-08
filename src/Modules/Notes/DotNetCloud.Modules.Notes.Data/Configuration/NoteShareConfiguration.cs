using DotNetCloud.Modules.Notes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Notes.Data.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="NoteShare"/>.
/// </summary>
public sealed class NoteShareConfiguration : IEntityTypeConfiguration<NoteShare>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NoteShare> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Permission)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.CreatedByUserId);
        builder.Property(s => s.UpdatedByUserId);

        // One share per note/target pair is enforced in code (user XOR team) — the
        // DB index is intentionally non-unique because team shares store Guid.Empty in
        // SharedWithUserId, which would otherwise collide on a (NoteId, UserId) unique key.
        builder.HasIndex(s => new { s.NoteId, s.SharedWithUserId })
            .HasDatabaseName("ix_note_shares_note_user");

        builder.HasIndex(s => s.SharedWithUserId)
            .HasDatabaseName("ix_note_shares_user_id");

        builder.HasIndex(s => new { s.NoteId, s.SharedWithTeamId })
            .HasDatabaseName("ix_note_shares_note_team");
    }
}
