namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Data transfer object for user information.
/// </summary>
public class UserDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user's display name.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the URL to the user's avatar.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the user's preferred locale (e.g., "en-US").
    /// </summary>
    public string Locale { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user's timezone (e.g., "UTC", "America/New_York").
    /// </summary>
    public string Timezone { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether the user account is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has email confirmed.
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// Gets or sets the date and time the user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the user's last login.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Gets or sets the user's assigned roles.
    /// </summary>
    public ICollection<string> Roles { get; set; } = new List<string>();
}

/// <summary>
/// Data transfer object for creating a new user.
/// </summary>
public class CreateUserDto
{
    /// <summary>
    /// Gets or sets the user's email address (required).
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user's password (required).
    /// </summary>
    public string Password { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user's display name (optional).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user's preferred locale (optional, defaults to "en-US").
    /// </summary>
    public string Locale { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets the user's timezone (optional, defaults to "UTC").
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Gets or sets the roles to assign to the new user (optional).
    /// </summary>
    public ICollection<string> Roles { get; set; } = new List<string>();
}

/// <summary>
/// Data transfer object for updating user information.
/// </summary>
public class UpdateUserDto
{
    /// <summary>
    /// Gets or sets the user's display name (optional).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the URL to the user's avatar (optional).
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the user's preferred locale (optional).
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    /// Gets or sets the user's timezone (optional).
    /// </summary>
    public string? Timezone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user account is active (admin only).
    /// </summary>
    public bool? IsActive { get; set; }
}
