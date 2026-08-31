using DotNetCloud.Core.Data.Entities.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Search;

/// <summary>
/// EF Core configuration for the <see cref="IndexingJob"/> entity.
/// </summary>
public sealed class IndexingJobConfiguration : IEntityTypeConfiguration<IndexingJob>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IndexingJob> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.ModuleId)
            .HasMaxLength(50);

        builder.Property(j => j.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(2000);

        // Query jobs by status
        builder.HasIndex(j => j.Status)
            .HasDatabaseName("ix_indexing_jobs_status");

        // Query jobs by module
        builder.HasIndex(j => j.ModuleId)
            .HasDatabaseName("ix_indexing_jobs_module_id");

        // The search module historically used PascalCase table/column names on BOTH
        // providers (unusual for this repo), and production data depends on those
        // exact names. Hardcode the "core" schema and table name here rather than
        // routing through ITableNamingStrategy, which would snake_case on PostgreSQL.
        builder.ToTable("IndexingJobs", "core");
    }
}
