using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.DTOs.Home
{
    /// <summary>
    /// Persists the visibility and position of a single Home-page widget slot.
    /// </summary>
    public sealed class HomeWidgetPreferenceItem
    {
        /// <summary>
        /// Gets the module identifier that owns the widget (for example <c>dotnetcloud.files</c>).
        /// </summary>
        public required string ModuleId { get; init; }

        /// <summary>
        /// Gets a value indicating whether the widget is shown. Defaults to <see langword="true"/>.
        /// </summary>
        public bool Visible { get; init; } = true;
    }

    /// <summary>
    /// Persists a user's Home-page widget layout: style, order and visibility.
    /// </summary>
    public sealed class HomeWidgetPreferences
    {
        /// <summary>
        /// The schema version written by the current client.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Gets the persisted schema version.
        /// </summary>
        public int Version { get; init; } = CurrentVersion;

        /// <summary>
        /// Gets the selected style token. See <see cref="HomeWidgetStyles"/>.
        /// </summary>
        public string Style { get; init; } = HomeWidgetStyles.DefaultToken;

        /// <summary>
        /// Gets the widget slots in display order.
        /// </summary>
        public List<HomeWidgetPreferenceItem> Items { get; init; } = [];
    }

    /// <summary>
    /// Defines the available Home-page widget styles. Tokens are the CSS <c>data-widget-style</c>
    /// values, stored verbatim so CSS identifiers can be renamed without a data migration.
    /// </summary>
    public static class HomeWidgetStyles
    {
        /// <summary>
        /// The original, unstyled widget treatment.
        /// </summary>
        public const string StrictlyBusiness = "strictly-business";

        /// <summary>
        /// Gradient skeuomorphic treatment where every module gets its own art direction.
        /// </summary>
        public const string ArtDepartment = "art-department";

        /// <summary>
        /// Neo-brutalist treatment: flat ink, hard edges, one geometric device per module.
        /// </summary>
        public const string HardCopy = "hard-copy";

        /// <summary>
        /// The style applied when a user has never chosen one.
        /// </summary>
        public const string DefaultToken = ArtDepartment;

        /// <summary>
        /// Gets all supported style tokens, in display order.
        /// </summary>
        public static IReadOnlyList<string> All { get; } = [StrictlyBusiness, ArtDepartment, HardCopy];

        /// <summary>
        /// Determines whether the supplied token is a supported style.
        /// </summary>
        /// <param name="token">The candidate style token.</param>
        /// <returns><see langword="true"/> when the token is supported; otherwise <see langword="false"/>.</returns>
        public static bool IsValid(string? token)
            => !string.IsNullOrWhiteSpace(token) && All.Contains(token);

        /// <summary>
        /// Returns the supplied token when it is supported, otherwise <see cref="DefaultToken"/>.
        /// </summary>
        /// <param name="token">The candidate style token.</param>
        /// <returns>A supported style token.</returns>
        public static string Normalize(string? token)
            => IsValid(token) ? token! : DefaultToken;

        /// <summary>
        /// Gets the human-readable display name for a style token.
        /// </summary>
        /// <param name="token">The style token.</param>
        /// <returns>The display name.</returns>
        public static string GetDisplayName(string? token) => Normalize(token) switch
        {
            StrictlyBusiness => "Strictly Business",
            HardCopy => "Hard Copy",
            _ => "Art Department",
        };

        /// <summary>
        /// Gets the one-line description shown next to a style option.
        /// </summary>
        /// <param name="token">The style token.</param>
        /// <returns>The description.</returns>
        public static string GetDescription(string? token) => Normalize(token) switch
        {
            StrictlyBusiness => "Clean and minimal. No surprises.",
            HardCopy => "Flat ink, hard edges, loud.",
            _ => "Every widget gets its own art direction.",
        };
    }
}

namespace DotNetCloud.Core.Services
{
    using DotNetCloud.Core.DTOs.Home;

    /// <summary>
    /// Loads and persists per-user Home-page widget preferences (style, order and visibility).
    /// </summary>
    public static class HomeWidgetPreferencesSettings
    {
        /// <summary>
        /// The settings module that owns Home-page widget preferences.
        /// </summary>
        public const string SettingsModule = "home-widgets";

        /// <summary>
        /// The settings key that holds the serialized preferences.
        /// </summary>
        public const string PreferencesKey = "preferences";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Loads a user's widget preferences, falling back to defaults when nothing is stored or the
        /// stored payload cannot be understood.
        /// </summary>
        /// <param name="settingsService">The user settings service.</param>
        /// <param name="userId">The authenticated user identifier.</param>
        /// <returns>The user's preferences, or defaults.</returns>
        public static async Task<HomeWidgetPreferences> LoadAsync(
            IUserSettingsService settingsService,
            Guid userId)
        {
            ArgumentNullException.ThrowIfNull(settingsService);

            var setting = await settingsService.GetSettingAsync(userId, SettingsModule, PreferencesKey);
            return Deserialize(setting?.Value);
        }

        /// <summary>
        /// Persists a user's widget preferences.
        /// </summary>
        /// <param name="settingsService">The user settings service.</param>
        /// <param name="userId">The authenticated user identifier.</param>
        /// <param name="preferences">The preferences to persist.</param>
        /// <param name="description">An optional settings description override.</param>
        public static async Task SaveAsync(
            IUserSettingsService settingsService,
            Guid userId,
            HomeWidgetPreferences preferences,
            string? description = null)
        {
            ArgumentNullException.ThrowIfNull(settingsService);
            ArgumentNullException.ThrowIfNull(preferences);

            await settingsService.UpsertSettingAsync(
                userId,
                SettingsModule,
                PreferencesKey,
                new UpsertUserSettingDto
                {
                    Value = Serialize(preferences),
                    Description = description ?? "Home-page widget layout (style, order, visibility)",
                });
        }

        /// <summary>
        /// Serializes widget preferences to JSON.
        /// </summary>
        /// <param name="preferences">The preferences to serialize.</param>
        /// <returns>The serialized payload.</returns>
        public static string Serialize(HomeWidgetPreferences preferences)
        {
            ArgumentNullException.ThrowIfNull(preferences);
            return JsonSerializer.Serialize(preferences, SerializerOptions);
        }

        /// <summary>
        /// Deserializes widget preferences, normalizing the style token and dropping malformed entries.
        /// Unknown style tokens and unreadable payloads fall back to defaults rather than throwing.
        /// </summary>
        /// <param name="value">The serialized payload, or <see langword="null"/>.</param>
        /// <returns>The deserialized preferences, or defaults.</returns>
        public static HomeWidgetPreferences Deserialize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new HomeWidgetPreferences();
            }

            HomeWidgetPreferences? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<HomeWidgetPreferences>(value, SerializerOptions);
            }
            catch (JsonException)
            {
                return new HomeWidgetPreferences();
            }

            // A payload written by a newer client may carry fields we do not understand — start fresh
            // rather than silently dropping the user into a partially applied layout.
            if (parsed is null || parsed.Version < 1 || parsed.Version > HomeWidgetPreferences.CurrentVersion)
            {
                return new HomeWidgetPreferences();
            }

            return new HomeWidgetPreferences
            {
                Version = HomeWidgetPreferences.CurrentVersion,
                Style = HomeWidgetStyles.Normalize(parsed.Style),
                Items = NormalizeItems(parsed.Items),
            };
        }

        private static List<HomeWidgetPreferenceItem> NormalizeItems(List<HomeWidgetPreferenceItem>? items)
        {
            if (items is null || items.Count == 0)
            {
                return [];
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalized = new List<HomeWidgetPreferenceItem>(items.Count);

            foreach (var item in items)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.ModuleId))
                {
                    continue;
                }

                var moduleId = item.ModuleId.Trim();
                if (!seen.Add(moduleId))
                {
                    continue;
                }

                normalized.Add(new HomeWidgetPreferenceItem
                {
                    ModuleId = moduleId,
                    Visible = item.Visible,
                });
            }

            return normalized;
        }
    }
}
