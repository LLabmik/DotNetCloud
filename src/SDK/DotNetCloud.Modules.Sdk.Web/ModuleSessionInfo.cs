namespace DotNetCloud.Modules.Sdk.Web;

/// <summary>
/// The acting user as a module UI sees it.
/// </summary>
public sealed class ModuleSessionInfo
{
    /// <summary>Gets the user id.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the display name, when the core could resolve one.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Gets the email address, when the user consented to the profile scope.</summary>
    public string? Email { get; init; }
}
