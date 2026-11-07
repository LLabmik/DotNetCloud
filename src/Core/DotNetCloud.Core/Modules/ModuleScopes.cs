namespace DotNetCloud.Core.Modules;

/// <summary>
/// Describes a single OAuth2 scope a module can request from a user.
/// </summary>
/// <param name="Name">The wire value, for example <c>files:read</c>.</param>
/// <param name="Resource">The resource the scope covers, for example <c>files</c>.</param>
/// <param name="Action">The action the scope allows (<c>read</c>, <c>write</c> or <c>send</c>).</param>
/// <param name="Description">
/// The plain-language sentence shown on the consent prompt and the "Connected apps" page.
/// Never derived from module-supplied text.
/// </param>
/// <param name="IsPersonalData">
/// <see langword="true"/> when the scope grants access to a user's personal data, so consent is
/// required. <see langword="false"/> for identity scopes that only carry the acting user's own claims.
/// </param>
public sealed record ModuleScopeDefinition(
    string Name,
    string Resource,
    string Action,
    string Description,
    bool IsPersonalData = true);

/// <summary>
/// The single source of truth for the scopes DotNetCloud understands.
/// </summary>
/// <remarks>
/// <para>
/// Before phase A3 the only scopes in existence (<c>files:read|write</c> and
/// <c>bookmarks:read|write</c>) were string literals duplicated in the OpenIddict scope registry and
/// in <c>OidcClientSeeder</c>. Everything that needs to know about a scope — the OpenIddict scope
/// registry, the first-party client seeder, module OIDC client registration, manifest validation,
/// the consent prompt, the "Connected apps" page and token-introspection filtering — reads this
/// catalog instead. Adding a scope means adding it here once.
/// </para>
/// <para>
/// The catalog is also the validation source for a v2 manifest's <c>userConsentScopes</c>: an
/// unknown scope is a manifest error, because a scope the core cannot describe is a scope the user
/// cannot meaningfully approve.
/// </para>
/// </remarks>
public static class ModuleScopes
{
    /// <summary>Read the user's name, email address, avatar and locale.</summary>
    public const string Profile = "profile";

    /// <summary>Read the user's files and folders.</summary>
    public const string FilesRead = "files:read";

    /// <summary>Create, change and delete the user's files and folders.</summary>
    public const string FilesWrite = "files:write";

    /// <summary>Read the user's contacts.</summary>
    public const string ContactsRead = "contacts:read";

    /// <summary>Create, change and delete the user's contacts.</summary>
    public const string ContactsWrite = "contacts:write";

    /// <summary>Read the user's calendars and events.</summary>
    public const string CalendarRead = "calendar:read";

    /// <summary>Create, change and delete the user's calendars and events.</summary>
    public const string CalendarWrite = "calendar:write";

    /// <summary>Read the user's notes.</summary>
    public const string NotesRead = "notes:read";

    /// <summary>Create, change and delete the user's notes.</summary>
    public const string NotesWrite = "notes:write";

    /// <summary>Read the user's mail.</summary>
    public const string MailRead = "mail:read";

    /// <summary>Send, change and delete the user's mail.</summary>
    public const string MailWrite = "mail:write";

    /// <summary>Read the user's chat conversations.</summary>
    public const string ChatRead = "chat:read";

    /// <summary>Send and change messages in the user's chat conversations.</summary>
    public const string ChatWrite = "chat:write";

    /// <summary>Read the user's Tracks boards, sprints and work items.</summary>
    public const string TracksRead = "tracks:read";

    /// <summary>Create, change and delete the user's Tracks items.</summary>
    public const string TracksWrite = "tracks:write";

    /// <summary>Read the user's photos.</summary>
    public const string PhotosRead = "photos:read";

    /// <summary>Upload, change and delete the user's photos.</summary>
    public const string PhotosWrite = "photos:write";

    /// <summary>Read the user's music library.</summary>
    public const string MusicRead = "music:read";

    /// <summary>Change the user's music library and playlists.</summary>
    public const string MusicWrite = "music:write";

    /// <summary>Read the user's videos.</summary>
    public const string VideoRead = "video:read";

    /// <summary>Upload, change and delete the user's videos.</summary>
    public const string VideoWrite = "video:write";

    /// <summary>Read the user's bookmarks.</summary>
    public const string BookmarksRead = "bookmarks:read";

    /// <summary>Create, change and delete the user's bookmarks.</summary>
    public const string BookmarksWrite = "bookmarks:write";

    /// <summary>Send notifications to the user.</summary>
    public const string NotificationsSend = "notifications:send";

    /// <summary>The OIDC identity scopes that are not themselves consent records.</summary>
    /// <remarks>
    /// <see cref="Profile"/> is intentionally absent: it is both an OIDC scope and a consent scope,
    /// and it is listed in <see cref="ConsentScopes"/>.
    /// </remarks>
    public static IReadOnlyList<string> StandardOidcScopes { get; } =
    [
        "openid",
        "email",
        "offline_access",
    ];

    /// <summary>Every scope a module may ask a user to consent to, in display order.</summary>
    public static IReadOnlyList<ModuleScopeDefinition> ConsentScopes { get; } =
    [
        new(Profile, "profile", "read", "Read your name, email address and profile picture."),
        new(FilesRead, "files", "read", "Read your files and folders."),
        new(FilesWrite, "files", "write", "Create, change and delete your files and folders."),
        new(ContactsRead, "contacts", "read", "Read your contacts."),
        new(ContactsWrite, "contacts", "write", "Create, change and delete your contacts."),
        new(CalendarRead, "calendar", "read", "Read your calendars and events."),
        new(CalendarWrite, "calendar", "write", "Create, change and delete your calendar events."),
        new(NotesRead, "notes", "read", "Read your notes."),
        new(NotesWrite, "notes", "write", "Create, change and delete your notes."),
        new(MailRead, "mail", "read", "Read your email."),
        new(MailWrite, "mail", "write", "Send, change and delete your email."),
        new(ChatRead, "chat", "read", "Read your chat conversations."),
        new(ChatWrite, "chat", "write", "Send messages in your chat conversations."),
        new(TracksRead, "tracks", "read", "Read your Tracks boards and work items."),
        new(TracksWrite, "tracks", "write", "Create and change your Tracks work items."),
        new(PhotosRead, "photos", "read", "Read your photos."),
        new(PhotosWrite, "photos", "write", "Upload, change and delete your photos."),
        new(MusicRead, "music", "read", "Read your music library."),
        new(MusicWrite, "music", "write", "Change your music library and playlists."),
        new(VideoRead, "video", "read", "Read your videos."),
        new(VideoWrite, "video", "write", "Upload, change and delete your videos."),
        new(BookmarksRead, "bookmarks", "read", "Read your bookmarks."),
        new(BookmarksWrite, "bookmarks", "write", "Create, change and delete your bookmarks."),
        new(NotificationsSend, "notifications", "send", "Send notifications to you.", IsPersonalData: false),
    ];

    private static readonly Dictionary<string, ModuleScopeDefinition> ByName =
        ConsentScopes.ToDictionary(scope => scope.Name, StringComparer.Ordinal);

    private static readonly HashSet<string> KnownNames =
        new(ConsentScopes.Select(scope => scope.Name).Concat(StandardOidcScopes), StringComparer.Ordinal);

    /// <summary>Every scope the core registers with OpenIddict (standard OIDC scopes first).</summary>
    public static IReadOnlyList<string> RegisteredScopes { get; } =
        ["openid", Profile, "email", "offline_access", .. ConsentScopes
            .Select(scope => scope.Name)
            .Where(name => name is not Profile)
            .ToList()];

    /// <summary>Determines whether the core knows the scope (standard OIDC or consent catalog).</summary>
    /// <param name="scope">The scope name.</param>
    /// <returns><see langword="true"/> when the scope is registered.</returns>
    public static bool IsKnown(string? scope) =>
        !string.IsNullOrWhiteSpace(scope) && KnownNames.Contains(scope);

    /// <summary>Determines whether the scope is one a user must consent to.</summary>
    /// <param name="scope">The scope name.</param>
    /// <returns><see langword="true"/> when the scope appears in <see cref="ConsentScopes"/>.</returns>
    public static bool IsConsentScope(string? scope) =>
        !string.IsNullOrWhiteSpace(scope) && ByName.ContainsKey(scope);

    /// <summary>Looks up a consent scope definition.</summary>
    /// <param name="scope">The scope name.</param>
    /// <returns>The definition, or <see langword="null"/> when the scope is not a consent scope.</returns>
    public static ModuleScopeDefinition? Find(string? scope) =>
        !string.IsNullOrWhiteSpace(scope) && ByName.TryGetValue(scope, out var definition) ? definition : null;

    /// <summary>
    /// Gets the plain-language description of a scope, falling back to the raw name for scopes the
    /// catalog does not describe (a manifest that passed an older validator, for example).
    /// </summary>
    /// <param name="scope">The scope name.</param>
    /// <returns>The description to show to a user.</returns>
    public static string Describe(string scope) =>
        Find(scope)?.Description ?? $"Access the '{scope}' permission.";

    /// <summary>
    /// Returns the subset of <paramref name="scopes"/> that are unknown to the catalog.
    /// </summary>
    /// <param name="scopes">The scopes to check.</param>
    /// <returns>The unknown scopes, in input order.</returns>
    public static IReadOnlyList<string> FindUnknown(IEnumerable<string> scopes) =>
        scopes.Where(scope => !IsKnown(scope)).Distinct(StringComparer.Ordinal).ToArray();
}

/// <summary>
/// Maps a capability to the user-consent scopes that capability can expose.
/// </summary>
/// <remarks>
/// This is the "permission mapping table": it answers "if a module asks for capability X, which
/// user data can it reach, and therefore which scopes must it declare and users consent to?".
/// Manifest validation (phase A3, F18) uses it to reject a module that requests a user-data
/// capability without declaring the matching scope.
/// </remarks>
public static class ModuleCapabilityScopes
{
    private static readonly Dictionary<string, IReadOnlyList<string>> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["IUserDirectory"] = [ModuleScopes.Profile],
            ["ICurrentUserContext"] = [ModuleScopes.Profile],
            ["IStorageProvider"] = [ModuleScopes.FilesRead],
            ["INotificationService"] = [ModuleScopes.NotificationsSend],
        };

    /// <summary>Gets the capability → required-scopes map.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> CapabilityToScopes => Map;

    /// <summary>Gets the consent scopes a capability can expose.</summary>
    /// <param name="capabilityName">The capability interface name.</param>
    /// <returns>The required scopes, or an empty list when the capability exposes no personal data.</returns>
    public static IReadOnlyList<string> RequiredFor(string? capabilityName) =>
        !string.IsNullOrWhiteSpace(capabilityName) && Map.TryGetValue(capabilityName, out var scopes)
            ? scopes
            : [];

    /// <summary>Gets the union of the consent scopes a set of capabilities requires.</summary>
    /// <param name="capabilityNames">The requested capability names.</param>
    /// <returns>The distinct required scopes.</returns>
    public static IReadOnlyList<string> RequiredFor(IEnumerable<string> capabilityNames) =>
        capabilityNames
            .SelectMany(RequiredFor)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
