using DotNetCloud.Modules.Search.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Search.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="SearchIndexEntry"/> entity.
/// Configures table structure, indexes, and constraints for the search index.
/// </summary>
public sealed class SearchIndexEntryConfiguration : IEntityTypeConfiguration<SearchIndexEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SearchIndexEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ModuleId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.EntityId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Content)
            .HasMaxLength(102400); // 100KB max indexed content

        builder.Property(e => e.Summary)
            .HasMaxLength(1000);

        builder.Property(e => e.OwnerId)
            .IsRequired();

        builder.Property(e => e.MetadataJson)
            .HasMaxLength(4000);

        // Composite unique: one entry per module+entity
        builder.HasIndex(e => new { e.ModuleId, e.EntityId })
            .IsUnique()
            .HasDatabaseName("ix_search_index_module_entity");

        // Permission-scoped queries
        builder.HasIndex(e => e.OwnerId)
            .HasDatabaseName("ix_search_index_owner_id");

        // Organization scoping
        builder.HasIndex(e => e.OrganizationId)
            .HasDatabaseName("ix_search_index_organization_id");

        // Module filter queries
        builder.HasIndex(e => e.ModuleId)
            .HasDatabaseName("ix_search_index_module_id");

        // Entity type filter
        builder.HasIndex(e => e.EntityType)
            .HasDatabaseName("ix_search_index_entity_type");

        // Date-sorted queries
        builder.HasIndex(e => e.UpdatedAt)
            .HasDatabaseName("ix_search_index_updated_at");
    }
}
