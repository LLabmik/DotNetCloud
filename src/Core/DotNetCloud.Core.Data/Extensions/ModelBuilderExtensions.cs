using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Frozen;

namespace DotNetCloud.Core.Data.Extensions;

/// <summary>
/// Extension methods for <see cref="ModelBuilder"/> that handle cross-database
/// column type compatibility.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// PostgreSQL-to-SQL Server column type mappings.
    /// When EF Core entity configurations specify PostgreSQL-native column types,
    /// this map converts them to the closest SQL Server equivalent.
    /// </summary>
    private static readonly FrozenDictionary<string, string> PostgresToSqlServerTypeMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bytea"] = "varbinary(max)",
            ["jsonb"] = "nvarchar(max)",
            ["text[]"] = "nvarchar(max)",
            ["uuid"] = "uniqueidentifier",
            ["timestamp with time zone"] = "datetimeoffset",
            ["timestamptz"] = "datetimeoffset",
        }.ToFrozenDictionary();

    /// <summary>
    /// Fixes PostgreSQL-specific column types on the model by replacing
    /// them with their SQL Server equivalents. Call this inside
    /// <see cref="DbContext.OnModelCreating(ModelBuilder)"/> after all entity
    /// configurations have been applied, when the database provider is SQL Server.
    /// </summary>
    /// <param name="modelBuilder">The model builder whose model to fix.</param>
    /// <remarks>
    /// This method inspects every entity property's explicitly configured column type
    /// (set via <c>HasColumnType()</c> in entity configurations). When a property has a
    /// PostgreSQL-native store type (e.g. <c>bytea</c>, <c>jsonb</c>), it is replaced
    /// with the appropriate SQL Server type (e.g. <c>varbinary(max)</c>,
    /// <c>nvarchar(max)</c>).
    /// </remarks>
    public static void FixColumnTypesForSqlServer(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                // Use the explicitly configured column type from the annotation,
                // which is what .HasColumnType() sets in entity configurations.
                var columnType = property.GetColumnType();
                if (string.IsNullOrEmpty(columnType))
                    continue;

                if (PostgresToSqlServerTypeMap.TryGetValue(columnType, out var sqlServerType))
                {
                    property.SetColumnType(sqlServerType);
                }
            }
        }
    }
}
