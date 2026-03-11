namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Stores server connection settings (URL, display name, account email) in app preferences.
/// Supports multiple server entries identified by base URL.
/// </summary>
public interface IServerConnectionStore
{
    /// <summary>Returns all saved server connections.</summary>
    IReadOnlyList<ServerConnection> GetAll();

    /// <summary>Saves or updates a server connection.</summary>
    void Save(ServerConnection connection);

    /// <summary>Removes a server connection by base URL.</summary>
    void Remove(string serverBaseUrl);

    /// <summary>Returns the currently active connection, or <c>null</c> if none.</summary>
    ServerConnection? GetActive();

    /// <summary>Sets the active server connection.</summary>
    void SetActive(string serverBaseUrl);
}

/// <summary>Identifies a saved DotNetCloud server connection.</summary>
/// <param name="ServerBaseUrl">Root URL including scheme and port, e.g. <c>https://cloud.example.com</c>.</param>
/// <param name="DisplayName">Friendly name shown in the UI.</param>
/// <param name="AccountEmail">Email address of the authenticated account.</param>
public sealed record ServerConnection(string ServerBaseUrl, string DisplayName, string AccountEmail);
