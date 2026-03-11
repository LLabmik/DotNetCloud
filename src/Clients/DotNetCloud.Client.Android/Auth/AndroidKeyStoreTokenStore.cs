namespace DotNetCloud.Client.Android.Auth;

/// <summary>
/// Stores OAuth2 tokens using <see cref="SecureStorage"/> which maps to Android Keystore on Android.
/// Keys are namespaced by server URL to support multiple-server accounts.
/// </summary>
internal sealed class AndroidKeyStoreTokenStore : ISecureTokenStore
{
    private static string AccessKey(string serverUrl) => $"dnc_at_{Uri.EscapeDataString(serverUrl)}";
    private static string RefreshKey(string serverUrl) => $"dnc_rt_{Uri.EscapeDataString(serverUrl)}";

    /// <inheritdoc />
    public async Task SaveTokensAsync(string serverUrl, string accessToken, string refreshToken, CancellationToken ct = default)
    {
        await SecureStorage.Default.SetAsync(AccessKey(serverUrl), accessToken).ConfigureAwait(false);
        await SecureStorage.Default.SetAsync(RefreshKey(serverUrl), refreshToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<string?> GetAccessTokenAsync(string serverUrl, CancellationToken ct = default) =>
        SecureStorage.Default.GetAsync(AccessKey(serverUrl));

    /// <inheritdoc />
    public Task<string?> GetRefreshTokenAsync(string serverUrl, CancellationToken ct = default) =>
        SecureStorage.Default.GetAsync(RefreshKey(serverUrl));

    /// <inheritdoc />
    public Task DeleteTokensAsync(string serverUrl, CancellationToken ct = default)
    {
        SecureStorage.Default.Remove(AccessKey(serverUrl));
        SecureStorage.Default.Remove(RefreshKey(serverUrl));
        return Task.CompletedTask;
    }
}
