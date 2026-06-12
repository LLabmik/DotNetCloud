using DotNetCloud.Core.Common;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DotNetCloud.Core.Data.ValueGeneration;

/// <summary>
/// Generates time-ordered GUIDs (UUIDv7) for entity primary keys in EF Core.
/// </summary>
/// <remarks>
/// <para>
/// This value generator produces UUIDv7 values — time-ordered GUIDs that encode a Unix
/// millisecond timestamp — ensuring monotonically increasing primary key values that
/// reduce index fragmentation compared to random UUIDv4 values.
/// </para>
/// <para>
/// It wraps <see cref="Guid.CreateVersion7()"/> via <see cref="GuidGenerator.NewSequentialGuid()"/>.
/// </para>
/// </remarks>
public sealed class SequentialGuidValueGenerator : ValueGenerator<Guid>
{
    /// <summary>
    /// Gets a value indicating whether the generated values are temporary.
    /// </summary>
    /// <value><c>false</c> — this generator produces real (non-temporary) values
    /// that are sent directly to the database.</value>
    public override bool GeneratesTemporaryValues => false;

    /// <summary>
    /// Generates a new UUIDv7 value.
    /// </summary>
    /// <param name="entry">The entity entry that the value is being generated for.</param>
    /// <returns>A time-ordered UUIDv7 value.</returns>
    public override Guid Next(EntityEntry entry)
    {
        return GuidGenerator.NewSequentialGuid();
    }
}
