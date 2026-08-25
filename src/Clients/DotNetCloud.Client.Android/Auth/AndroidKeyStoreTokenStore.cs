using System.Globalization;

namespace DotNetCloud.Client.Android.Auth;

/// <summary>
/// Stores OAuth2 tokens using <see cref="SecureStorage"/> which maps to Android Keystore on Android.
/// Keys are namespaced by server URL to support multiple-server accounts.
/// </summary>
internal sealed class AndroidKeyStoreTokenStore : ISecureTokenStore
{
    private static string AccessKey(string serverUrl) => $"dnc_at_{Uri.EscapeDataString(serverUrl)}";
    private static string RefreshKey(string serverUrl) => $"dnc_rt_{Uri.EscapeDataString(serverUrl)}";
    private static string IdTokenKey(string serverUrl) => $"dnc_id_{Uri.EscapeDataString(serverUrl)}";
    private static string ExpiryKey(string serverUrl) => $"dnc_exp_{Uri.EscapeDataString(serverUrl)}";

    /// <inheritdoc />
    public async Task SaveTokensAsync(string serverUrl, string accessToken, string refreshToken, CancellationToken ct = default)
    {
        await SecureStorage.Default.SetAsync(AccessKey(serverUrl), accessToken).ConfigureAwait(false);
        await SecureStorage.Default.SetAsync(RefreshKey(serverUrl), refreshToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveTokensAsync(string serverUrl, string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        await SaveTokensAsync(serverUrl, accessToken, refreshToken, ct).ConfigureAwait(false);
        await SecureStorage.Default.SetAsync(ExpiryKey(serverUrl), FormatExpiry(expiresAt)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveTokensAsync(string serverUrl, string accessToken, string refreshToken, string? idToken, CancellationToken ct = default)
    {
        await SaveTokensAsync(serverUrl, accessToken, refreshToken, ct).ConfigureAwait(false);
        if (idToken is not null)
            await SecureStorage.Default.SetAsync(IdTokenKey(serverUrl), idToken).ConfigureAwait(false);
        else
            SecureStorage.Default.Remove(IdTokenKey(serverUrl));
    }

    /// <inheritdoc />
    public async Task SaveTokensAsync(string serverUrl, string accessToken, string refreshToken, string? idToken, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        await SaveTokensAsync(serverUrl, accessToken, refreshToken, expiresAt, ct).ConfigureAwait(false);
        if (idToken is not null)
            await SecureStorage.Default.SetAsync(IdTokenKey(serverUrl), idToken).ConfigureAwait(false);
        else
            SecureStorage.Default.Remove(IdTokenKey(serverUrl));
    }

    /// <inheritdoc />
    public Task<string?> GetAccessTokenAsync(string serverUrl, CancellationToken ct = default) =>
        SecureStorage.Default.GetAsync(AccessKey(serverUrl));

    /// <inheritdoc />
    public Task<string?> GetRefreshTokenAsync(string serverUrl, CancellationToken ct = default) =>
        SecureStorage.Default.GetAsync(RefreshKey(serverUrl));

    /// <inheritdoc />
    public Task<string?> GetIdTokenAsync(string serverUrl, CancellationToken ct = default) =>
        SecureStorage.Default.GetAsync(IdTokenKey(serverUrl));

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetAccessTokenExpiryAsync(string serverUrl, CancellationToken ct = default)
    {
        var value = await SecureStorage.Default.GetAsync(ExpiryKey(serverUrl)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiry)
            ? expiry
            : null;
    }

    /// <inheritdoc />
    public Task DeleteTokensAsync(string serverUrl, CancellationToken ct = default)
    {
        SecureStorage.Default.Remove(AccessKey(serverUrl));
        SecureStorage.Default.Remove(RefreshKey(serverUrl));
        SecureStorage.Default.Remove(IdTokenKey(serverUrl));
        SecureStorage.Default.Remove(ExpiryKey(serverUrl));
        return Task.CompletedTask;
    }

    private static string FormatExpiry(DateTimeOffset expiresAt) =>
        expiresAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
