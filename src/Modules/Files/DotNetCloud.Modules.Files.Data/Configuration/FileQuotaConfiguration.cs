using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="FileQuota"/> entity.
/// </summary>
public sealed class FileQuotaConfiguration : IEntityTypeConfiguration<FileQuota>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileQuota> builder)
    {
        builder.HasKey(q => q.Id);

        builder.Property(q => q.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(q => q.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(q => q.LastCalculatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Ignore computed properties
        builder.Ignore(q => q.UsagePercent);
        builder.Ignore(q => q.RemainingBytes);

        // One quota per user
        builder.HasIndex(q => q.UserId)
            .IsUnique()
            .HasDatabaseName("ix_file_quotas_user_id");
    }
}
