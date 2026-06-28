using System.Text.Json;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Persists server connection settings using <see cref="Preferences"/> (maps to
/// Android SharedPreferences on Android).
/// </summary>
internal sealed class PreferenceServerConnectionStore : IServerConnectionStore
{
    private const string ListKey = "dnc_server_list";
    private const string ActiveKey = "dnc_server_active";
    private const string ActiveDataKey = "dnc_server_active_data";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public IReadOnlyList<ServerConnection> GetAll()
    {
        var json = Preferences.Default.Get(ListKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return [];
        return JsonSerializer.Deserialize<List<ServerConnection>>(json, JsonOptions) ?? [];
    }

    /// <inheritdoc />
    public void Save(ServerConnection connection)
    {
        var list = new List<ServerConnection>(GetAll());
        var idx = list.FindIndex(s => s.ServerBaseUrl == connection.ServerBaseUrl);
        if (idx >= 0)
            list[idx] = connection;
        else
            list.Add(connection);
        Preferences.Default.Set(ListKey, JsonSerializer.Serialize(list, JsonOptions));

        // Also keep the active connection data in sync
        if (Preferences.Default.Get(ActiveKey, string.Empty) == connection.ServerBaseUrl)
            Preferences.Default.Set(ActiveDataKey, JsonSerializer.Serialize(connection, JsonOptions));
    }

    /// <inheritdoc />
    public void Remove(string serverBaseUrl)
    {
        var list = new List<ServerConnection>(GetAll());
        list.RemoveAll(s => s.ServerBaseUrl == serverBaseUrl);
        Preferences.Default.Set(ListKey, JsonSerializer.Serialize(list, JsonOptions));

        if (Preferences.Default.Get(ActiveKey, string.Empty) == serverBaseUrl)
        {
            Preferences.Default.Remove(ActiveKey);
            Preferences.Default.Remove(ActiveDataKey);
        }
    }

    /// <inheritdoc />
    public ServerConnection? GetActive()
    {
        // Fast path: read the full connection JSON directly (saved by SetActive).
        var activeData = Preferences.Default.Get(ActiveDataKey, string.Empty);
        if (!string.IsNullOrEmpty(activeData))
        {
            var conn = JsonSerializer.Deserialize<ServerConnection>(activeData, JsonOptions);
            if (conn is not null)
                return conn;
        }

        // Fallback: read from the URL-only key and cross-reference with the list
        // (for users upgrading from an older version of the app).
        var activeUrl = Preferences.Default.Get(ActiveKey, string.Empty);
        if (string.IsNullOrEmpty(activeUrl))
            return null;
        return GetAll().FirstOrDefault(s => s.ServerBaseUrl == activeUrl);
    }

    /// <inheritdoc />
    public void SetActive(string serverBaseUrl)
    {
        Preferences.Default.Set(ActiveKey, serverBaseUrl);

        // Look up the full connection in the list and cache it in ActiveDataKey
        // so GetActive() can serve it directly on next startup.
        var conn = GetAll().FirstOrDefault(s => s.ServerBaseUrl == serverBaseUrl);
        if (conn is not null)
            Preferences.Default.Set(ActiveDataKey, JsonSerializer.Serialize(conn, JsonOptions));
    }
}
