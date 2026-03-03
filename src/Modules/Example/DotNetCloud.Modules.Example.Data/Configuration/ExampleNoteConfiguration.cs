using DotNetCloud.Modules.Example.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Example.Data.Configuration;

/// <summary>
/// EF Core fluent API configuration for the <see cref="ExampleNote"/> entity.
/// </summary>
public sealed class ExampleNoteConfiguration : IEntityTypeConfiguration<ExampleNote>
{
    /// <summary>
    /// Configures the ExampleNote entity mapping, constraints, and indexes.
    /// </summary>
    public void Configure(EntityTypeBuilder<ExampleNote> builder)
    {
        // Table naming:
        // PostgreSQL: example.example_notes
        // SQL Server: [example].[ExampleNotes]
        // MariaDB: example_example_notes

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Content)
            .HasMaxLength(10000);

        builder.Property(n => n.CreatedByUserId)
            .IsRequired();

        builder.Property(n => n.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(n => n.UpdatedAt);

        // Indexes
        builder.HasIndex(n => n.CreatedByUserId)
            .HasDatabaseName("ix_example_notes_created_by_user_id");

        builder.HasIndex(n => n.CreatedAt)
            .HasDatabaseName("ix_example_notes_created_at");

        builder.HasIndex(n => n.Title)
            .HasDatabaseName("ix_example_notes_title");
    }
}
