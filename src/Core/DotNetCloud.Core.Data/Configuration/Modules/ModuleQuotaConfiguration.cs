using DotNetCloud.Core.Data.Entities.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Modules;

/// <summary>
/// EF Core configuration for the ModuleQuota entity.
/// </summary>
public class ModuleQuotaConfiguration : IEntityTypeConfiguration<ModuleQuota>
{
    /// <summary>
    /// Configures the ModuleQuota entity mapping, constraints, and indexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<ModuleQuota> builder)
    {
        builder.HasKey(q => q.ModuleId);

        builder.Property(q => q.ModuleId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(q => q.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(q => q.UpdatedByUserId)
            .IsRequired(false);

        builder.HasOne(q => q.Module)
            .WithOne()
            .HasForeignKey<ModuleQuota>(q => q.ModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_module_quotas_module_id");
    }
}
