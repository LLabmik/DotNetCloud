using System.Text;
using System.Text.Json;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Extracts the authenticated user identifier from a JWT access token.
/// Prefers the standard <c>sub</c> claim and expects a GUID value.
/// </summary>
internal static class AccessTokenUserIdExtractor
{
    /// <summary>
    /// Extracts the caller user ID from a bearer access token.
    /// </summary>
    /// <param name="accessToken">JWT access token string.</param>
    /// <returns>User ID parsed from token claims.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the token does not contain a GUID user ID.</exception>
    public static Guid ExtractUserId(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Access token is required to resolve userId query parameter.");

        var parts = accessToken.Split('.');
        if (parts.Length < 2)
            throw new InvalidOperationException("Invalid access token format.");

        var payloadJson = DecodeBase64Url(parts[1]);
        using var doc = JsonDocument.Parse(payloadJson);

        if (doc.RootElement.TryGetProperty("sub", out var subElement) &&
            Guid.TryParse(subElement.GetString(), out var subGuid))
        {
            return subGuid;
        }

        if (doc.RootElement.TryGetProperty("user_id", out var userIdElement) &&
            Guid.TryParse(userIdElement.GetString(), out var userIdGuid))
        {
            return userIdGuid;
        }

        throw new InvalidOperationException("Access token did not contain a GUID user identifier claim.");
    }

    private static string DecodeBase64Url(string value)
    {
        value = value.Replace('-', '+').Replace('_', '/');
        value = value.PadRight(value.Length + (4 - value.Length % 4) % 4, '=');
        var bytes = Convert.FromBase64String(value);
        return Encoding.UTF8.GetString(bytes);
    }
}
