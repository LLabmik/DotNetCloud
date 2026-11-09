namespace DotNetCloud.Core.Modules;

/// <summary>
/// Protects secret module settings at rest.
/// </summary>
/// <remarks>
/// <para>
/// A module setting marked as secret (an API key, a provider token) must never be stored as plain
/// text. The core-side implementation is backed by the core DataProtection key ring, and the scope
/// is per module, so ciphertext written for one module cannot be decrypted in another module's
/// context even if the rows are swapped.
/// </para>
/// <para>
/// This interface deliberately lives beside <see cref="IModuleSettingsStore"/> rather than in the
/// data project: the data project has no DataProtection dependency, and a test or a CLI host can
/// supply its own implementation.
/// </para>
/// </remarks>
public interface IModuleSettingCipher
{
    /// <summary>Protects a plain-text value for storage under a module-scoped purpose.</summary>
    /// <param name="moduleId">The module that owns the setting.</param>
    /// <param name="plainText">The value to protect.</param>
    /// <returns>Ciphertext, safe to persist.</returns>
    string Protect(string moduleId, string plainText);

    /// <summary>Reverts <see cref="Protect"/>.</summary>
    /// <param name="moduleId">The module that owns the setting.</param>
    /// <param name="cipherText">The stored ciphertext.</param>
    /// <returns>The original plain-text value.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The value was not produced by this key ring for this module.
    /// </exception>
    string Unprotect(string moduleId, string cipherText);
}
