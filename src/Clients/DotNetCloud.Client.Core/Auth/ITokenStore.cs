namespace DotNetCloud.Client.Core.Auth;

/// <summary>
/// Securely stores and retrieves OAuth2 tokens per account.
/// </summary>
public interface ITokenStore
{
    /// <summary>Saves tokens for the given account key.</summary>
    Task SaveAsync(string accountKey, TokenInfo tokens, CancellationToken cancellationToken = default);

    /// <summary>Loads tokens for the given account key. Returns null if not found.</summary>
    Task<TokenInfo?> LoadAsync(string accountKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes tokens for the given account key.</summary>
    Task DeleteAsync(string accountKey, CancellationToken cancellationToken = default);
}
