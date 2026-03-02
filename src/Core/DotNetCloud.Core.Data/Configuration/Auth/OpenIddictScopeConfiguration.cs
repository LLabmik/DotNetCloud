using DotNetCloud.Core.Data.Entities.Auth;
using DotNetCloud.Core.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Auth;

/// <summary>
/// Entity Framework Core configuration for the <see cref="OpenIddictScope"/> entity.
/// </summary>
/// <remarks>
/// Configures table name, primary key, indexes, and column constraints
/// for OAuth2/OIDC scope definitions.
/// </remarks>
public class OpenIddictScopeConfiguration : IEntityTypeConfiguration<OpenIddictScope>
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIddictScopeConfiguration"/> class.
    /// </summary>
    /// <param name="namingStrategy">The table naming strategy for multi-database support.</param>
    public OpenIddictScopeConfiguration(ITableNamingStrategy namingStrategy)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>
    /// Configures the <see cref="OpenIddictScope"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<OpenIddictScope> builder)
    {
        // Table name
        builder.ToTable(_namingStrategy.GetTableName("OpenIddictScopes"), _namingStrategy.GetSchemaName("core"));

        // Primary key
        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.Id)
            .HasColumnName(_namingStrategy.GetColumnName("Id"))
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ConcurrencyToken)
            .HasColumnName(_namingStrategy.GetColumnName("ConcurrencyToken"))
            .HasMaxLength(50)
            .IsConcurrencyToken();

        builder.Property(e => e.Description)
            .HasColumnName(_namingStrategy.GetColumnName("Description"))
            .HasMaxLength(500);

        builder.Property(e => e.Descriptions)
            .HasColumnName(_namingStrategy.GetColumnName("Descriptions"))
            .HasMaxLength(2000);

        builder.Property(e => e.DisplayName)
            .HasColumnName(_namingStrategy.GetColumnName("DisplayName"))
            .HasMaxLength(200);

        builder.Property(e => e.DisplayNames)
            .HasColumnName(_namingStrategy.GetColumnName("DisplayNames"))
            .HasMaxLength(2000);

        builder.Property(e => e.Name)
            .HasColumnName(_namingStrategy.GetColumnName("Name"))
            .HasMaxLength(200);

        builder.Property(e => e.Properties)
            .HasColumnName(_namingStrategy.GetColumnName("Properties"));

        builder.Property(e => e.Resources)
            .HasColumnName(_namingStrategy.GetColumnName("Resources"));

        // Indexes
        builder.HasIndex(e => e.Name)
            .IsUnique()
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictScopes", "Name", isUnique: true));
    }
}
