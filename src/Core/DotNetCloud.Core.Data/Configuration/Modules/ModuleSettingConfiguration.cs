using DotNetCloud.Core.Data.Entities.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Modules;

/// <summary>
/// EF Core configuration for the ModuleSetting entity.
/// </summary>
public class ModuleSettingConfiguration : IEntityTypeConfiguration<ModuleSetting>
{
    /// <summary>
    /// Configures the ModuleSetting entity mapping, constraints, and indexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<ModuleSetting> builder)
    {
        builder.HasKey(s => new { s.ModuleId, s.Key });

        builder.Property(s => s.ModuleId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Value)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(s => s.IsSecret)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.UpdatedByUserId)
            .IsRequired(false);

        builder.HasIndex(s => s.ModuleId)
            .HasDatabaseName("ix_module_settings_module_id");

        builder.HasOne(s => s.Module)
            .WithMany()
            .HasForeignKey(s => s.ModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_module_settings_module_id");
    }
}
