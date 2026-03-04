namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Validates WOPI proof key signatures from Collabora Online requests.
/// Proof key validation confirms that WOPI requests originated from the trusted Collabora server.
/// </summary>
/// <remarks>
/// Collabora adds X-WOPI-Proof (current key signature) and X-WOPI-Proof-Old (previous key, for rotation)
/// headers to every WOPI request. The signed payload is the concatenation of:
/// [access_token_length][access_token][url_length][url][8][timestamp].
/// </remarks>
public interface IWopiProofKeyValidator
{
    /// <summary>
    /// Validates the WOPI proof key signature on an incoming Collabora request.
    /// </summary>
    /// <param name="accessToken">The WOPI access token from the request query string.</param>
    /// <param name="requestUrl">The full URL of the WOPI request (used verbatim in the proof).</param>
    /// <param name="proof">The X-WOPI-Proof header value (current key signature).</param>
    /// <param name="proofOld">The X-WOPI-Proof-Old header value (previous key signature, for key rotation).</param>
    /// <param name="timestamp">The X-WOPI-TimeStamp header value (Windows FILETIME ticks as a string).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the proof is valid and the timestamp is within the allowed window;
    /// <c>false</c> if validation fails or proof keys are unavailable.
    /// </returns>
    Task<bool> ValidateAsync(
        string accessToken,
        string requestUrl,
        string? proof,
        string? proofOld,
        string? timestamp,
        CancellationToken cancellationToken = default);
}
