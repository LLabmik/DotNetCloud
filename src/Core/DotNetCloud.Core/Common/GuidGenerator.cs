namespace DotNetCloud.Core.Common;

/// <summary>
/// Provides methods for generating time-ordered GUIDs (UUIDv7).
/// </summary>
/// <remarks>
/// <para>
/// UUIDv7 is a time-ordered UUID format that encodes a Unix millisecond timestamp
/// in the first 48 bits, followed by version and variant bits, and random data.
/// Unlike UUIDv4 (random), UUIDv7 values are monotonically sortable by creation time,
/// which significantly reduces index fragmentation in databases — especially important
/// for clustered primary key indexes in SQL Server and B-tree indexes in PostgreSQL.
/// </para>
/// <para>
/// .NET 9+ provides <see cref="Guid.CreateVersion7()"/> natively, which generates
/// UUIDv7 values. This class wraps that method for consistent usage across the codebase.
/// </para>
/// <para>
/// <b>Usage guidelines:</b>
/// <list type="bullet">
///   <item><description><b>New entity IDs (C# code):</b> Always use <see cref="NewSequentialGuid()"/>.</description></item>
///   <item><description><b>EF Core value generation:</b> Use <c>SequentialGuidValueGenerator</c> via the model builder helper.</description></item>
///   <item><description><b>Test data:</b> Use <see cref="NewSequentialGuid()"/> for consistency with production.</description></item>
///   <item><description><b>Database defaults (MSSQL only):</b> Use <c>NEWSEQUENTIALID()</c> as a fallback.</description></item>
/// </list>
/// </para>
/// </remarks>
public static class GuidGenerator
{
    /// <summary>
    /// Generates a new time-ordered GUID (UUIDv7).
    /// </summary>
    /// <returns>A UUIDv7 value that is monotonically sortable by creation time.</returns>
    /// <remarks>
    /// Wraps <see cref="Guid.CreateVersion7()"/> which is available starting from .NET 9.
    /// The returned GUID encodes the current UTC timestamp (millisecond precision) in its
    /// first 48 bits, enabling efficient B-tree index performance.
    /// </remarks>
    public static Guid NewSequentialGuid()
    {
        return Guid.CreateVersion7();
    }

    /// <summary>
    /// Generates a new time-ordered GUID (UUIDv7) with a specific timestamp.
    /// </summary>
    /// <param name="timestamp">The timestamp to encode in the GUID.</param>
    /// <returns>A UUIDv7 value encoding the specified timestamp.</returns>
    /// <remarks>
    /// Useful for deterministic GUID generation in tests or when backdating entity IDs.
    /// </remarks>
    public static Guid NewSequentialGuid(DateTimeOffset timestamp)
    {
        return Guid.CreateVersion7(timestamp);
    }
}
