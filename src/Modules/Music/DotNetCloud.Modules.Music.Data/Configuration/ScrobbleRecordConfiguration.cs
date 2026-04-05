using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ScrobbleRecord"/> entity.
/// </summary>
public sealed class ScrobbleRecordConfiguration : IEntityTypeConfiguration<ScrobbleRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ScrobbleRecord> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ArtistName).IsRequired().HasMaxLength(500);
        builder.Property(s => s.TrackTitle).IsRequired().HasMaxLength(500);
        builder.Property(s => s.AlbumTitle).HasMaxLength(500);
        builder.Property(s => s.ScrobbledAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(s => s.Track)
            .WithMany()
            .HasForeignKey(s => s.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.UserId, s.ScrobbledAt }).HasDatabaseName("ix_scrobble_records_user_scrobbled_at");
        builder.HasIndex(s => s.TrackId).HasDatabaseName("ix_scrobble_records_track_id");
    }
}
