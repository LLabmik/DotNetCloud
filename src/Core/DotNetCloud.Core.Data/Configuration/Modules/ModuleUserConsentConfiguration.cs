using DotNetCloud.Core.Data.Entities.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Modules;

/// <summary>
/// EF Core configuration for the ModuleUserConsent entity.
/// </summary>
public class ModuleUserConsentConfiguration : IEntityTypeConfiguration<ModuleUserConsent>
{
    /// <summary>
    /// Configures the ModuleUserConsent entity mapping, constraints, and indexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<ModuleUserConsent> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(c => c.ModuleId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.UserId)
            .IsRequired();

        builder.Property(c => c.Scope)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.GrantedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.ExpiresAtUtc)
            .IsRequired(false);

        builder.Property(c => c.GrantedByUserId)
            .IsRequired(false);

        // Exactly one consent row per (module, user, scope).
        builder.HasIndex(c => new { c.ModuleId, c.UserId, c.Scope })
            .IsUnique()
            .HasDatabaseName("uq_module_user_consents_module_id_user_id_scope");

        builder.HasIndex(c => c.UserId)
            .HasDatabaseName("ix_module_user_consents_user_id");

        builder.HasIndex(c => c.ModuleId)
            .HasDatabaseName("ix_module_user_consents_module_id");

        builder.HasOne(c => c.Module)
            .WithMany(m => m.UserConsents)
            .HasForeignKey(c => c.ModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_module_user_consents_module_id");

        // Erasure must remove consent rows with the user, and the table must never block a delete.
        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_module_user_consents_user_id");

        builder.HasOne(c => c.GrantedByUser)
            .WithMany()
            .HasForeignKey(c => c.GrantedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_module_user_consents_granted_by_user_id");
    }
}
