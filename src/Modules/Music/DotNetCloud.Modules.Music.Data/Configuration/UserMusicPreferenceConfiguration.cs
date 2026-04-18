using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="UserMusicPreference"/> entity.
/// </summary>
public sealed class UserMusicPreferenceConfiguration : IEntityTypeConfiguration<UserMusicPreference>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserMusicPreference> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(p => p.UserId).IsUnique().HasDatabaseName("uq_user_music_preferences_user_id");

        builder.HasOne(p => p.ActiveEqPreset)
            .WithMany()
            .HasForeignKey(p => p.ActiveEqPresetId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
