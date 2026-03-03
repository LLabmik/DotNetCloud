namespace DotNetCloud.Core.Services;

/// <summary>
/// Manages FIDO2/WebAuthn passkey credentials for passwordless authentication.
/// </summary>
/// <remarks>
/// This is a skeleton interface for Phase 0.4. Return types are <see cref="object"/> pending
/// a proper FIDO2 library integration in a future phase. Implementations should replace
/// <see cref="object"/> with strongly-typed FIDO2 option and assertion types.
/// </remarks>
public interface IFidoService
{
    /// <summary>
    /// Generates credential creation options for registering a new passkey.
    /// </summary>
    /// <param name="userId">The ID of the user registering the passkey.</param>
    /// <returns>Credential creation options to send to the authenticator.</returns>
    Task<object> GetRegistrationOptionsAsync(Guid userId);

    /// <summary>
    /// Completes passkey registration by validating the authenticator's response.
    /// </summary>
    /// <param name="userId">The ID of the user completing registration.</param>
    /// <param name="response">The authenticator attestation response.</param>
    /// <returns><see langword="true"/> if registration succeeded; otherwise <see langword="false"/>.</returns>
    Task<bool> CompleteRegistrationAsync(Guid userId, object response);

    /// <summary>
    /// Generates assertion options for authenticating with a registered passkey.
    /// </summary>
    /// <param name="userHandle">The user handle identifying the authenticating user.</param>
    /// <returns>Assertion options to send to the authenticator.</returns>
    Task<object> GetAssertionOptionsAsync(string userHandle);

    /// <summary>
    /// Completes passkey authentication by validating the authenticator's assertion.
    /// </summary>
    /// <param name="response">The authenticator assertion response.</param>
    /// <returns><see langword="true"/> if authentication succeeded; otherwise <see langword="false"/>.</returns>
    Task<bool> CompleteAssertionAsync(object response);
}
