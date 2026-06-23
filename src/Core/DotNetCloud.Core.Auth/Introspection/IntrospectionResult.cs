using System.Security.Claims;

namespace DotNetCloud.Core.Auth.Introspection;

/// Result of a token introspection request.
/// Maps directly to the gRPC IntrospectTokenResponse.
public sealed class IntrospectionResult
{
    /// <summary>Whether the token is currently valid.</summary>
    public bool Active { get; init; }

    /// <summary>The subject (user ID) of the token.</summary>
    public string? Sub { get; init; }

    /// <summary>The user's display name.</summary>
    public string? Username { get; init; }

    /// <summary>The user's email address.</summary>
    public string? Email { get; init; }

    /// <summary>Scopes granted by this token (filtered to module's declared scopes).</summary>
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    /// <summary>All claims from the validated token.</summary>
    public IReadOnlyList<Claim> Claims { get; init; } = Array.Empty<Claim>();

    /// <summary>If not active, a human-readable reason.</summary>
    public string? ErrorDescription { get; init; }

    /// <summary>Converts the introspection result into a ClaimsPrincipal.</summary>
    public ClaimsPrincipal ToPrincipal(string authenticationType = "Introspection")
    {
        if (!Active || string.IsNullOrEmpty(Sub))
            throw new InvalidOperationException("Cannot create principal from inactive introspection result.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Sub),
        };

        if (!string.IsNullOrEmpty(Username))
            claims.Add(new Claim(ClaimTypes.Name, Username));

        if (!string.IsNullOrEmpty(Email))
            claims.Add(new Claim(ClaimTypes.Email, Email));

        // Add all scopes as individual claims for policy-based authorization.
        foreach (var scope in Scopes)
            claims.Add(new Claim("scope", scope));

        // Add all original claims from introspection.
        claims.AddRange(Claims);

        var identity = new ClaimsIdentity(claims, authenticationType);
        return new ClaimsPrincipal(identity);
    }
}
