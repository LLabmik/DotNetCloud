namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Validates password strength for CLI setup prompts.
/// </summary>
internal static class PasswordValidator
{
    private const int MinLength = 10;

    private static readonly string[] CommonPasswords =
    [
        "password", "password1", "password123", "123456", "12345678",
        "1234567890", "qwerty", "abc123", "letmein", "admin",
        "welcome", "monkey", "dragon", "master", "login",
        "changeme", "dotnetcloud", "postgres", "root"
    ];

    /// <summary>
    /// Validates a password and returns a failure reason, or <c>null</c> if acceptable.
    /// </summary>
    /// <param name="password">The password to validate.</param>
    /// <param name="forbiddenPasswords">
    /// Additional passwords that must not be reused (e.g., the database password).
    /// </param>
    public static string? Validate(string password, params ReadOnlySpan<string> forbiddenPasswords)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password cannot be empty.";
        }

        if (password.Length < MinLength)
        {
            return $"Password must be at least {MinLength} characters long.";
        }

        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;
        var hasSpecial = false;

        foreach (var c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else hasSpecial = true;
        }

        var categoryCount = (hasUpper ? 1 : 0) + (hasLower ? 1 : 0)
                          + (hasDigit ? 1 : 0) + (hasSpecial ? 1 : 0);

        if (categoryCount < 3)
        {
            return "Password must include at least 3 of: uppercase, lowercase, digit, special character.";
        }

        if (Array.Exists(CommonPasswords,
            p => password.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return "That password is too common. Please choose something less guessable.";
        }

        // Reject passwords that match other credentials (e.g., the database password).
        // Case-insensitive — "MyPass" and "mypass" are effectively the same password.
        foreach (var forbidden in forbiddenPasswords)
        {
            if (!string.IsNullOrEmpty(forbidden)
                && string.Equals(password, forbidden, StringComparison.OrdinalIgnoreCase))
            {
                return "You cannot use the same password for your database and your login account. Please choose a different password.";
            }
        }

        return null;
    }
}
