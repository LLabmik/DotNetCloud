using DotNetCloud.Core.Data.Entities.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Modules;

/// <summary>
/// EF Core configuration for the ModuleDataStore entity.
/// </summary>
public class ModuleDataStoreConfiguration : IEntityTypeConfiguration<ModuleDataStore>
{
    /// <summary>
    /// Configures the ModuleDataStore entity mapping, constraints, and indexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<ModuleDataStore> builder)
    {
        builder.HasKey(d => d.ModuleId);

        builder.Property(d => d.ModuleId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.SchemaName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.PrincipalName)
            .IsRequired()
            .HasMaxLength(200);

        // DataProtection ciphertext; no length limit needed but keep it bounded for SQL Server.
        builder.Property(d => d.EncryptedConnectionString)
            .IsRequired(false)
            .HasMaxLength(4000);

        builder.Property(d => d.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(d => d.LastError)
            .IsRequired(false)
            .HasMaxLength(2000);

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(d => d.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(d => d.Status)
            .HasDatabaseName("ix_module_data_stores_status");

        builder.HasOne(d => d.Module)
            .WithOne()
            .HasForeignKey<ModuleDataStore>(d => d.ModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_module_data_stores_module_id");
    }
}
