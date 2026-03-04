namespace DotNetCloud.UI.Android.Services;

/// <summary>
/// Handles user authentication and token management.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>Whether the user is currently authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The current user's ID.</summary>
    Guid? CurrentUserId { get; }

    /// <summary>The current access token.</summary>
    string? AccessToken { get; }

    /// <summary>Authenticates with username/password.</summary>
    Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Signs out and clears tokens.</summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>Refreshes the access token using stored refresh token.</summary>
    Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Stub authentication service for initial skeleton.
/// </summary>
internal sealed class AuthenticationService : IAuthenticationService
{
    /// <inheritdoc />
    public bool IsAuthenticated { get; private set; }

    /// <inheritdoc />
    public Guid? CurrentUserId { get; private set; }

    /// <inheritdoc />
    public string? AccessToken { get; private set; }

    /// <inheritdoc />
    public Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        // Stub: always succeeds with a generated user ID
        CurrentUserId = Guid.NewGuid();
        AccessToken = $"stub-token-{CurrentUserId}";
        IsAuthenticated = true;
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        CurrentUserId = null;
        AccessToken = null;
        IsAuthenticated = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsAuthenticated);
    }
}
