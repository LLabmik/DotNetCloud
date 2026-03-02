using DotNetCloud.Core.Data.Entities.Auth;
using DotNetCloud.Core.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Auth;

/// <summary>
/// Entity Framework Core configuration for the <see cref="OpenIddictToken"/> entity.
/// </summary>
/// <remarks>
/// Configures table name, primary key, indexes, relationships, and column constraints
/// for OAuth2/OIDC tokens (access tokens, refresh tokens, ID tokens, authorization codes).
/// </remarks>
public class OpenIddictTokenConfiguration : IEntityTypeConfiguration<OpenIddictToken>
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIddictTokenConfiguration"/> class.
    /// </summary>
    /// <param name="namingStrategy">The table naming strategy for multi-database support.</param>
    public OpenIddictTokenConfiguration(ITableNamingStrategy namingStrategy)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>
    /// Configures the <see cref="OpenIddictToken"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<OpenIddictToken> builder)
    {
        // Table name
        builder.ToTable(_namingStrategy.GetTableName("OpenIddictTokens"), _namingStrategy.GetSchemaName("core"));

        // Primary key
        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.Id)
            .HasColumnName(_namingStrategy.GetColumnName("Id"))
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ApplicationId)
            .HasColumnName(_namingStrategy.GetColumnName("ApplicationId"));

        builder.Property(e => e.AuthorizationId)
            .HasColumnName(_namingStrategy.GetColumnName("AuthorizationId"));

        builder.Property(e => e.ConcurrencyToken)
            .HasColumnName(_namingStrategy.GetColumnName("ConcurrencyToken"))
            .HasMaxLength(50)
            .IsConcurrencyToken();

        builder.Property(e => e.CreationDate)
            .HasColumnName(_namingStrategy.GetColumnName("CreationDate"));

        builder.Property(e => e.ExpirationDate)
            .HasColumnName(_namingStrategy.GetColumnName("ExpirationDate"));

        builder.Property(e => e.Payload)
            .HasColumnName(_namingStrategy.GetColumnName("Payload"));

        builder.Property(e => e.Properties)
            .HasColumnName(_namingStrategy.GetColumnName("Properties"));

        builder.Property(e => e.RedemptionDate)
            .HasColumnName(_namingStrategy.GetColumnName("RedemptionDate"));

        builder.Property(e => e.ReferenceId)
            .HasColumnName(_namingStrategy.GetColumnName("ReferenceId"))
            .HasMaxLength(200);

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
        builder.HasIndex(e => e.ReferenceId)
            .IsUnique()
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictTokens", "ReferenceId", isUnique: true));

        builder.HasIndex(e => e.ApplicationId)
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictTokens", "ApplicationId"));

        builder.HasIndex(e => e.AuthorizationId)
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictTokens", "AuthorizationId"));

        builder.HasIndex(e => e.Subject)
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictTokens", "Subject"));

        builder.HasIndex(e => e.Status)
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictTokens", "Status"));

        builder.HasIndex(e => e.Type)
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictTokens", "Type"));

        builder.HasIndex(e => e.ExpirationDate)
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictTokens", "ExpirationDate"));

        builder.HasIndex(e => new { e.ApplicationId, e.Status, e.Subject, e.Type })
            .HasDatabaseName(_namingStrategy.GetIndexName("OpenIddictTokens", "ApplicationId_Status_Subject_Type"));

        // Relationships
        builder.HasOne(e => e.Application)
            .WithMany(e => e.Tokens)
            .HasForeignKey(e => e.ApplicationId)
            .HasConstraintName(_namingStrategy.GetForeignKeyName("OpenIddictTokens", "OpenIddictApplications", "ApplicationId"))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Authorization)
            .WithMany(e => e.Tokens)
            .HasForeignKey(e => e.AuthorizationId)
            .HasConstraintName(_namingStrategy.GetForeignKeyName("OpenIddictTokens", "OpenIddictAuthorizations", "AuthorizationId"))
            .OnDelete(DeleteBehavior.Cascade);
    }
}
