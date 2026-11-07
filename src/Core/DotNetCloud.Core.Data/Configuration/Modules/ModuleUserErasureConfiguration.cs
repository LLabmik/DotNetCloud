using DotNetCloud.Core.Data.Entities.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Modules;

/// <summary>
/// EF Core configuration for the ModuleUserErasure entity.
/// </summary>
public class ModuleUserErasureConfiguration : IEntityTypeConfiguration<ModuleUserErasure>
{
    /// <summary>
    /// Configures the ModuleUserErasure entity mapping, constraints, and indexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<ModuleUserErasure> builder)
    {
        builder.HasKey(e => new { e.RequestId, e.ModuleId });

        builder.Property(e => e.ModuleId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(e => e.Attempts)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.LastError)
            .IsRequired(false)
            .HasMaxLength(2000);

        builder.Property(e => e.CompletedAtUtc)
            .IsRequired(false);

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_module_user_erasures_status");
    }
}
