using DotNetCloud.Core.Modules;
using Microsoft.AspNetCore.DataProtection;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// DataProtection-backed <see cref="IModuleSettingCipher"/> for the module settings plane.
/// </summary>
/// <remarks>
/// The purpose string is fixed; the module id becomes a sub-purpose, so ciphertext written for one
/// module cannot be read back under another module's identity even if a row is moved between them.
/// Unlike the data-store protector this one never leaves the core: only the core process reads or
/// writes module settings, and a module receives plain text through the <c>GetSetting</c> RPC after
/// the store has decrypted it.
/// </remarks>
public sealed class ModuleSettingCipher : IModuleSettingCipher
{
    /// <summary>DataProtection purpose for secret module settings.</summary>
    internal const string ProtectorPurpose = "DotNetCloud.ModuleSettings.Secret";

    private readonly IDataProtector _protector;

    /// <summary>Initializes a new instance of the <see cref="ModuleSettingCipher"/> class.</summary>
    /// <param name="dataProtection">The core DataProtection provider (the shared key ring).</param>
    public ModuleSettingCipher(IDataProtectionProvider dataProtection)
    {
        ArgumentNullException.ThrowIfNull(dataProtection);
        _protector = dataProtection.CreateProtector(ProtectorPurpose);
    }

    /// <inheritdoc />
    public string Protect(string moduleId, string plainText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(plainText);

        return _protector.CreateProtector(moduleId).Protect(plainText);
    }

    /// <inheritdoc />
    public string Unprotect(string moduleId, string cipherText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(cipherText);

        return _protector.CreateProtector(moduleId).Unprotect(cipherText);
    }
}
