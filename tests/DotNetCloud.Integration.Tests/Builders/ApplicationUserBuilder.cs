using DotNetCloud.Core.Data.Entities.Identity;

namespace DotNetCloud.Integration.Tests.Builders;

/// <summary>
/// Fluent builder for creating <see cref="ApplicationUser"/> test instances.
/// </summary>
internal sealed class ApplicationUserBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _email = $"user-{Guid.NewGuid():N}@test.local";
    private string _displayName = "Test User";
    private string _locale = "en-US";
    private string _timezone = "UTC";
    private bool _isActive = true;
    private bool _emailConfirmed = true;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _lastLoginAt;
    private string? _avatarUrl;

    public ApplicationUserBuilder WithId(Guid id) { _id = id; return this; }
    public ApplicationUserBuilder WithEmail(string email) { _email = email; return this; }
    public ApplicationUserBuilder WithDisplayName(string name) { _displayName = name; return this; }
    public ApplicationUserBuilder WithLocale(string locale) { _locale = locale; return this; }
    public ApplicationUserBuilder WithTimezone(string tz) { _timezone = tz; return this; }
    public ApplicationUserBuilder WithIsActive(bool active) { _isActive = active; return this; }
    public ApplicationUserBuilder WithEmailConfirmed(bool confirmed) { _emailConfirmed = confirmed; return this; }
    public ApplicationUserBuilder WithCreatedAt(DateTime dt) { _createdAt = dt; return this; }
    public ApplicationUserBuilder WithLastLoginAt(DateTime? dt) { _lastLoginAt = dt; return this; }
    public ApplicationUserBuilder WithAvatarUrl(string? url) { _avatarUrl = url; return this; }

    public ApplicationUser Build()
    {
        return new ApplicationUser
        {
            Id = _id,
            UserName = _email,
            NormalizedUserName = _email.ToUpperInvariant(),
            Email = _email,
            NormalizedEmail = _email.ToUpperInvariant(),
            DisplayName = _displayName,
            Locale = _locale,
            Timezone = _timezone,
            IsActive = _isActive,
            EmailConfirmed = _emailConfirmed,
            CreatedAt = _createdAt,
            LastLoginAt = _lastLoginAt,
            AvatarUrl = _avatarUrl,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
    }

    /// <summary>
    /// Creates a default admin user for integration tests.
    /// </summary>
    public static ApplicationUser CreateAdmin(string email = "admin@test.local")
    {
        return new ApplicationUserBuilder()
            .WithEmail(email)
            .WithDisplayName("Test Admin")
            .Build();
    }
}
