using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Music.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EqPreset"/> entity.
/// </summary>
public sealed class EqPresetConfiguration : IEntityTypeConfiguration<EqPreset>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EqPreset> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.BandsJson).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(e => e.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(e => e.OwnerId).HasDatabaseName("ix_eq_presets_owner_id");
        builder.HasIndex(e => new { e.OwnerId, e.Name }).HasDatabaseName("ix_eq_presets_owner_name");
    }
}
