using DotNetCloud.Core.Data.Entities.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Modules;

/// <summary>
/// EF Core configuration for the UserErasureRequest entity.
/// </summary>
public class UserErasureRequestConfiguration : IEntityTypeConfiguration<UserErasureRequest>
{
    /// <summary>
    /// Configures the UserErasureRequest entity mapping, constraints, and indexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<UserErasureRequest> builder)
    {
        builder.HasKey(r => r.RequestId);

        builder.Property(r => r.UserId)
            .IsRequired();

        builder.Property(r => r.RequestedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(r => r.RequestedByUserId)
            .IsRequired(false);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(r => r.CompletedAtUtc)
            .IsRequired(false);

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("ix_user_erasure_requests_user_id");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_user_erasure_requests_status");

        builder.HasMany(r => r.Modules)
            .WithOne(m => m.Request)
            .HasForeignKey(m => m.RequestId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_module_user_erasures_request_id");
    }
}
