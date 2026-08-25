using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Core.Auth;

/// <summary>
/// User profile claims returned by the OIDC <c>/connect/userinfo</c> endpoint.
/// </summary>
public sealed record UserProfileInfo
{
    /// <summary>Subject identifier (the user's GUID on the server).</summary>
    [JsonPropertyName("sub")]
    public string? Subject { get; init; }

    /// <summary>Human-readable display name (e.g. <c>Ben K.</c>).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Login username (e.g. <c>benk</c>).</summary>
    [JsonPropertyName("preferred_username")]
    public string? PreferredUsername { get; init; }

    /// <summary>Email address (may be omitted when the <c>email</c> scope is not granted).</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }
}
