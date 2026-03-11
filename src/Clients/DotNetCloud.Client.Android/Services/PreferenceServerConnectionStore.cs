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

    /// <inheritdoc />
    public IReadOnlyList<ServerConnection> GetAll()
    {
        var json = Preferences.Default.Get(ListKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return [];
        return JsonSerializer.Deserialize<List<ServerConnection>>(json) ?? [];
    }

    /// <inheritdoc />
    public void Save(ServerConnection connection)
    {
        var list = new List<ServerConnection>(GetAll());
        var idx = list.FindIndex(s => s.ServerBaseUrl == connection.ServerBaseUrl);
        if (idx >= 0) list[idx] = connection;
        else list.Add(connection);
        Preferences.Default.Set(ListKey, JsonSerializer.Serialize(list));
    }

    /// <inheritdoc />
    public void Remove(string serverBaseUrl)
    {
        var list = new List<ServerConnection>(GetAll());
        list.RemoveAll(s => s.ServerBaseUrl == serverBaseUrl);
        Preferences.Default.Set(ListKey, JsonSerializer.Serialize(list));

        if (Preferences.Default.Get(ActiveKey, string.Empty) == serverBaseUrl)
            Preferences.Default.Remove(ActiveKey);
    }

    /// <inheritdoc />
    public ServerConnection? GetActive()
    {
        var activeUrl = Preferences.Default.Get(ActiveKey, string.Empty);
        if (string.IsNullOrEmpty(activeUrl)) return null;
        return GetAll().FirstOrDefault(s => s.ServerBaseUrl == activeUrl);
    }

    /// <inheritdoc />
    public void SetActive(string serverBaseUrl) =>
        Preferences.Default.Set(ActiveKey, serverBaseUrl);
}
