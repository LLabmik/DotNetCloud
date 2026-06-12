using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.ValueGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DotNetCloud.Core.Data.Configuration.Extensions;

/// <summary>
/// Extension methods for applying sequential GUID (UUIDv7) configuration to EF Core models.
/// </summary>
public static class SequentialGuidConfigurationExtensions
{
    /// <summary>
    /// Applies UUIDv7 (time-ordered GUID) generation to all Guid primary key properties
    /// in the model.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="provider">The active database provider.</param>
    /// <remarks>
    /// <para>
    /// For every entity type in the model, this method locates all single-column
    /// <see cref="Guid"/> primary keys and:
    /// <list type="bullet">
    ///   <item><description>Sets <see cref="SequentialGuidValueGenerator"/> as the client-side value generator
    ///   (produces UUIDv7 values before insert).</description></item>
    ///   <item><description>For <see cref="DatabaseProvider.SqlServer"/>, also adds
    ///   <c>DEFAULT NEWSEQUENTIALID()</c> as the database column default as a fallback for
    ///   direct SQL inserts.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This method should be called in each DbContext's <c>OnModelCreating</c> after all
    /// entity configurations have been applied.
    /// </para>
    /// </remarks>
    public static void ApplySequentialGuidDefaults(ModelBuilder modelBuilder, DatabaseProvider provider)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey == null)
                continue;

            // Only handle single-column primary keys of type Guid
            if (primaryKey.Properties.Count != 1)
                continue;

            var property = primaryKey.Properties[0];
            if (property.ClrType != typeof(Guid))
                continue;

            // Apply the client-side UUIDv7 value generator
            var propertyBuilder = modelBuilder
                .Entity(entityType.ClrType)
                .Property(property.Name);

            propertyBuilder.HasValueGenerator<SequentialGuidValueGenerator>();

            // For SQL Server, add NEWSEQUENTIALID() as the database default
            if (provider == DatabaseProvider.SqlServer)
            {
                propertyBuilder.HasDefaultValueSql("NEWSEQUENTIALID()");
            }
        }
    }
}
