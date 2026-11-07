namespace DotNetCloud.Core.DTOs;

/// <summary>One scope a user approved for an application, in plain language.</summary>
/// <param name="Scope">The wire value, for example <c>files:read</c>.</param>
/// <param name="Description">The sentence shown to the user.</param>
public sealed record ConsentScopeDto(string Scope, string Description);

/// <summary>
/// A module that holds consent from the current user, as listed on "Connected apps".
/// </summary>
public sealed class ConnectedAppDto
{
    /// <summary>The module id.</summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>The module's display name.</summary>
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>The approved scopes with their descriptions.</summary>
    public IReadOnlyList<ConsentScopeDto> Scopes { get; set; } = [];

    /// <summary>When the most recent scope was approved.</summary>
    public DateTime? GrantedAtUtc { get; set; }

    /// <summary>Whether the module is still installed.</summary>
    public bool IsModuleInstalled { get; set; }
}

/// <summary>One scope of a module with the number of users who approved it (admin view).</summary>
public sealed class ModuleScopeConsentDto
{
    /// <summary>The scope.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>The plain-language description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>How many users hold an active consent for it.</summary>
    public int UserCount { get; set; }
}

/// <summary>One user's consent record for a module (admin view).</summary>
public sealed class ModuleConsentUserDto
{
    /// <summary>The user id.</summary>
    public Guid UserId { get; set; }

    /// <summary>The approved scopes.</summary>
    public IReadOnlyList<string> Scopes { get; set; } = [];

    /// <summary>When the most recent scope was approved.</summary>
    public DateTime? GrantedAtUtc { get; set; }

    /// <summary>Whether an administrator granted on the user's behalf.</summary>
    public bool GrantedByAdmin { get; set; }
}

/// <summary>Consent overview for one module (admin view).</summary>
public sealed class ModuleConsentOverviewDto
{
    /// <summary>The module id.</summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>How many distinct users consented to at least one scope.</summary>
    public int ConsentingUsers { get; set; }

    /// <summary>Per-scope consented user counts, including scopes nobody has approved yet.</summary>
    public IReadOnlyList<ModuleScopeConsentDto> Scopes { get; set; } = [];

    /// <summary>The users who consented.</summary>
    public IReadOnlyList<ModuleConsentUserDto> Users { get; set; } = [];
}
