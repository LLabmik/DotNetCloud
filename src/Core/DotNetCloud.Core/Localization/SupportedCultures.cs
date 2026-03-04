using System.Globalization;

namespace DotNetCloud.Core.Localization;

/// <summary>
/// Defines the cultures supported by the DotNetCloud application.
/// Used by both server-side and client-side localization configuration.
/// </summary>
public static class SupportedCultures
{
    /// <summary>
    /// The default culture used when no user preference is set.
    /// </summary>
    public const string DefaultCulture = "en-US";

    /// <summary>
    /// BCP-47 language tags for all supported cultures.
    /// Add new languages here as translations become available.
    /// </summary>
    public static readonly string[] All =
    [
        "en-US",  // English (United States) — default
        "es-ES",  // Spanish (Spain)
        "de-DE",  // German (Germany)
        "fr-FR",  // French (France)
        "pt-BR",  // Portuguese (Brazil)
        "ja-JP",  // Japanese (Japan)
        "zh-CN",  // Chinese (Simplified, China)
    ];

    /// <summary>
    /// Display names for each supported culture, keyed by BCP-47 tag.
    /// Used in the culture selector UI because WASM globalization data
    /// may not include localized culture display names.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en-US"] = "English (United States)",
            ["es-ES"] = "Español (España)",
            ["de-DE"] = "Deutsch (Deutschland)",
            ["fr-FR"] = "Français (France)",
            ["pt-BR"] = "Português (Brasil)",
            ["ja-JP"] = "日本語 (日本)",
            ["zh-CN"] = "中文 (简体, 中国)",
        };

    /// <summary>
    /// Returns <see cref="CultureInfo"/> instances for all supported cultures.
    /// </summary>
    public static CultureInfo[] GetCultureInfos() =>
        All.Select(c => new CultureInfo(c)).ToArray();
}
