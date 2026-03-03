using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Core.Data.Entities.Auth;

/// <summary>
/// Represents a FIDO2/WebAuthn passkey credential registered by a user.
/// </summary>
/// <remarks>
/// WebAuthn credentials enable passwordless authentication using hardware security keys,
/// biometric authenticators, or platform authenticators (e.g., Windows Hello, Face ID).
/// Each credential is device-specific and bound to a specific relying party origin.
/// </remarks>
public class FidoCredential
{
    /// <summary>
    /// Gets or sets the unique identifier for this credential record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who registered this credential.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the raw credential ID returned by the authenticator during registration.
    /// </summary>
    /// <remarks>
    /// This value uniquely identifies the credential on the authenticator device.
    /// It is used to look up the credential during authentication assertions.
    /// </remarks>
    public byte[] CredentialId { get; set; } = [];

    /// <summary>
    /// Gets or sets the COSE-encoded public key associated with this credential.
    /// </summary>
    /// <remarks>
    /// The public key is used to verify authentication assertion signatures.
    /// It is never transmitted to the client after registration.
    /// </remarks>
    public byte[] PublicKey { get; set; } = [];

    /// <summary>
    /// Gets or sets the signature counter value from the last successful authentication.
    /// </summary>
    /// <remarks>
    /// The counter is incremented by the authenticator on each use.
    /// A counter value less than or equal to the stored value may indicate a cloned authenticator.
    /// </remarks>
    public uint SignatureCounter { get; set; }

    /// <summary>
    /// Gets or sets a user-provided name for this authenticator device.
    /// </summary>
    /// <remarks>
    /// Examples: "YubiKey 5", "Windows Hello on Surface Pro", "iPhone Face ID".
    /// Allows users to distinguish between multiple registered authenticators.
    /// </remarks>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when this credential was registered.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the user who registered this credential.
    /// </summary>
    public virtual ApplicationUser? User { get; set; }
}
