using DotNetCloud.Core.Data.Entities.Auth;
using DotNetCloud.Core.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Auth;

/// <summary>
/// Entity Framework Core configuration for the <see cref="OpenIddictAuthorization"/> entity.
/// </summary>
/// <remarks>
/// Configures table name, primary key, indexes, relationships, and column constraints
/// for OAuth2/OIDC user authorizations (consent records).
/// </remarks>
public class OpenIddictAuthorizationConfiguration : IEntityTypeConfiguration<OpenIddictAuthorization>
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIddictAuthorizationConfiguration"/> class.
    /// </summary>
    /// <param name="namingStrategy">The table naming strategy for multi-database support.</param>
    public OpenIddictAuthorizationConfiguration(ITableNamingStrategy namingStrategy)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>
    /// Configures the <see cref="OpenIddictAuthorization"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<OpenIddictAuthorization> builder)
    {
        // Table name
        builder.ToTable(_namingStrategy.GetTableName("OpenIddictAuthorizations"), _namingStrategy.GetSchemaName("core"));

        // Primary key
        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.Id)
            .HasColumnName(_namingStrategy.GetColumnName("Id"))
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ApplicationId)
            .HasColumnName(_namingStrategy.GetColumnName("ApplicationId"));

        builder.Property(e => e.ConcurrencyToken)
            .HasColumnName(_namingStrategy.GetColumnName("ConcurrencyToken"))
            .HasMaxLength(50)
            .IsConcurrencyToken();

        builder.Property(e => e.CreationDate)
            .HasColumnName(_namingStrategy.GetColumnName("CreationDate"));

        builder.Property(e => e.Properties)
            .HasColumnName(_namingStrategy.GetColumnName("Properties"));

        builder.Property(e => e.Scopes)
            .HasColumnName(_namingStrategy.GetColumnName("Scopes"));

        builder.Property(e => e.Status)
            .HasColumnName(_namingStrategy.GetColumnName("Status"))
            .HasMaxLength(50);

        builder.Property(e => e.Subject)
            .HasColumnName(_namingStrategy.GetColumnName("Subject"))
            .HasMaxLength(200);

        builder.Property(e => e.Type)
            .HasColumnName(_namingStrategy.GetColumnName("Type"))
            .HasMaxLength(50);

        // Indexes
        builder.HasIndex(e => e.ApplicationId)
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictAuthorizations", "ApplicationId"));

        builder.HasIndex(e => e.Subject)
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictAuthorizations", "Subject"));

        builder.HasIndex(e => e.Status)
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictAuthorizations", "Status"));

        builder.HasIndex(e => new { e.ApplicationId, e.Subject, e.Status })
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictAuthorizations", "ApplicationId_Subject_Status"));

        // Relationships
        builder.HasOne(e => e.Application)
            .WithMany(e => e.Authorizations)
            .HasForeignKey(e => e.ApplicationId)
            .HasConstraintName(_namingStrategy.GetForeignKeyName("OpenIddictAuthorizations", "OpenIddictApplications", "ApplicationId"))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Tokens)
            .WithOne(e => e.Authorization!)
            .HasForeignKey(e => e.AuthorizationId)
            .HasConstraintName(_namingStrategy.GetForeignKeyName("OpenIddictAuthorizations", "OpenIddictTokens", "AuthorizationId"))
            .OnDelete(DeleteBehavior.Cascade);
    }
}
