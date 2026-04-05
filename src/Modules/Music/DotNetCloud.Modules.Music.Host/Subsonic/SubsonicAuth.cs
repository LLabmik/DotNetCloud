using System.Security.Cryptography;
using System.Text;

namespace DotNetCloud.Modules.Music.Host.Subsonic;

/// <summary>
/// Subsonic API authentication helpers.
/// Supports token-based authentication (MD5(password + salt)) and plain password.
/// </summary>
public static class SubsonicAuth
{
    /// <summary>
    /// Validates Subsonic token-based authentication.
    /// The client sends: token = MD5(password + salt), salt = random string.
    /// We compute MD5(storedPassword + salt) and compare.
    /// </summary>
    /// <param name="token">The MD5 token sent by the client.</param>
    /// <param name="salt">The salt sent by the client.</param>
    /// <param name="storedPassword">The user's stored app-password.</param>
    /// <returns>True if authentication succeeds.</returns>
    public static bool ValidateToken(string token, string salt, string storedPassword)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(storedPassword))
            return false;

        var expectedToken = ComputeMd5(storedPassword + salt);
        return string.Equals(token, expectedToken, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Computes MD5 hash of a string.
    /// </summary>
    internal static string ComputeMd5(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }
}
