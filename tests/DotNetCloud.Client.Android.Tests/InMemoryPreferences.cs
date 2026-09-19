using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests;

/// <summary>
/// In-memory <see cref="IAppPreferences"/> used by the background-poll tests, so no MAUI preference
/// store is touched.
/// </summary>
internal sealed class InMemoryPreferences : IAppPreferences
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <summary>Number of writes performed, for assertions about state churn.</summary>
    public int WriteCount { get; private set; }

    /// <inheritdoc />
    public T Get<T>(string key, T defaultValue) =>
        _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

    /// <inheritdoc />
    public void Set<T>(string key, T value)
    {
        _values[key] = value;
        WriteCount++;
    }
}
