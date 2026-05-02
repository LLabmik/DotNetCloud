using Microsoft.AspNetCore.DataProtection;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Encrypts and decrypts email account credentials using ASP.NET Core Data Protection.
/// Uses per-user additional entropy to isolate protection scopes.
/// </summary>
public sealed class EmailCredentialEncryptionService
{
    private readonly IDataProtector _baseProtector;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailCredentialEncryptionService"/> class.
    /// </summary>
    public EmailCredentialEncryptionService(IDataProtectionProvider provider)
    {
        _baseProtector = provider.CreateProtector("DotNetCloud.Modules.Email.Credentials");
    }

    /// <summary>
    /// Encrypts a credential payload for storage. Uses per-user entropy for additional isolation.
    /// </summary>
    public string Protect(byte[] credentialBytes, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(credentialBytes);
        var userProtector = _baseProtector.CreateProtector(userId.ToString("N"));
        var protectedBytes = userProtector.Protect(credentialBytes);
        return Convert.ToBase64String(protectedBytes);
    }

    /// <summary>
    /// Decrypts a stored credential payload.
    /// </summary>
    public byte[] Unprotect(string protectedPayload, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(protectedPayload);
        var userProtector = _baseProtector.CreateProtector(userId.ToString("N"));
        var protectedBytes = Convert.FromBase64String(protectedPayload);
        return userProtector.Unprotect(protectedBytes);
    }
}
