using DotNetCloud.Core.Data.Entities.Auth;
using DotNetCloud.Core.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Auth;

/// <summary>
/// Entity Framework Core configuration for the <see cref="OpenIddictApplication"/> entity.
/// </summary>
/// <remarks>
/// Configures table name, primary key, indexes, relationships, and column constraints
/// for OAuth2/OIDC client applications.
/// </remarks>
public class OpenIddictApplicationConfiguration : IEntityTypeConfiguration<OpenIddictApplication>
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIddictApplicationConfiguration"/> class.
    /// </summary>
    /// <param name="namingStrategy">The table naming strategy for multi-database support.</param>
    public OpenIddictApplicationConfiguration(ITableNamingStrategy namingStrategy)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>
    /// Configures the <see cref="OpenIddictApplication"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<OpenIddictApplication> builder)
    {
        // Table name
        builder.ToTable(_namingStrategy.GetTableName("OpenIddictApplications"), _namingStrategy.GetSchemaName("core"));

        // Primary key
        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.Id)
            .HasColumnName(_namingStrategy.GetColumnName("Id"))
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ClientId)
            .HasColumnName(_namingStrategy.GetColumnName("ClientId"))
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ClientSecret)
            .HasColumnName(_namingStrategy.GetColumnName("ClientSecret"))
            .HasMaxLength(500);

        builder.Property(e => e.ConcurrencyToken)
            .HasColumnName(_namingStrategy.GetColumnName("ConcurrencyToken"))
            .HasMaxLength(50)
            .IsConcurrencyToken();

        builder.Property(e => e.ConsentType)
            .HasColumnName(_namingStrategy.GetColumnName("ConsentType"))
            .HasMaxLength(50);

        builder.Property(e => e.DisplayName)
            .HasColumnName(_namingStrategy.GetColumnName("DisplayName"))
            .HasMaxLength(200);

        builder.Property(e => e.DisplayNames)
            .HasColumnName(_namingStrategy.GetColumnName("DisplayNames"))
            .HasMaxLength(2000);

        builder.Property(e => e.JsonWebKeySet)
            .HasColumnName(_namingStrategy.GetColumnName("JsonWebKeySet"));

        builder.Property(e => e.Permissions)
            .HasColumnName(_namingStrategy.GetColumnName("Permissions"));

        builder.Property(e => e.PostLogoutRedirectUris)
            .HasColumnName(_namingStrategy.GetColumnName("PostLogoutRedirectUris"));

        builder.Property(e => e.Properties)
            .HasColumnName(_namingStrategy.GetColumnName("Properties"));

        builder.Property(e => e.RedirectUris)
            .HasColumnName(_namingStrategy.GetColumnName("RedirectUris"));

        builder.Property(e => e.Requirements)
            .HasColumnName(_namingStrategy.GetColumnName("Requirements"));

        builder.Property(e => e.Type)
            .HasColumnName(_namingStrategy.GetColumnName("Type"))
            .HasMaxLength(50);

        builder.Property(e => e.Settings)
            .HasColumnName(_namingStrategy.GetColumnName("Settings"));

        // Indexes
        builder.HasIndex(e => e.ClientId)
            .IsUnique()
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictApplications", "ClientId", isUnique: true));

        // Relationships
        builder.HasMany(e => e.Authorizations)
            .WithOne(e => e.Application!)
            .HasForeignKey(e => e.ApplicationId)
            .HasConstraintName(_namingStrategy.GetForeignKeyName("OpenIddictApplications", "OpenIddictAuthorizations", "ApplicationId"))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Tokens)
            .WithOne(e => e.Application!)
            .HasForeignKey(e => e.ApplicationId)
            .HasConstraintName(_namingStrategy.GetForeignKeyName("OpenIddictApplications", "OpenIddictTokens", "ApplicationId"))
            .OnDelete(DeleteBehavior.Cascade);
    }
}
