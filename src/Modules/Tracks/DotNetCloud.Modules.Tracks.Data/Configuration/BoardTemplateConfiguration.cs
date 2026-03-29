using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="BoardTemplate"/> entity.
/// </summary>
public sealed class BoardTemplateConfiguration : IEntityTypeConfiguration<BoardTemplate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BoardTemplate> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.Category)
            .HasMaxLength(100);

        builder.Property(t => t.DefinitionJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(t => t.IsBuiltIn);
        builder.HasIndex(t => t.CreatedByUserId);
    }
}
