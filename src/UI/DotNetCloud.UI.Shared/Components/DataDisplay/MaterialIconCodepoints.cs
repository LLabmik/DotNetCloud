namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>
/// Maps Material Icons ligature names to their Unicode Private Use Area codepoints.
/// When the font's ligature feature doesn't work, we can render the codepoint character directly.
/// </summary>
public static class MaterialIconCodepoints
{
    /// <summary>
    /// Returns the Unicode character for the given Material Icons ligature name.
    /// Falls back to rendering the name as a ligature if the codepoint isn't known.
    /// </summary>
    public static char GetChar(string iconName)
    {
        if (s_map.TryGetValue(iconName, out var cp))
            return (char)cp;
        return '\0'; // Signal to use ligature rendering
    }

    /// <summary>
    /// Whether a direct codepoint mapping exists for the given icon name.
    /// </summary>
    public static bool HasCodepoint(string iconName) => s_map.ContainsKey(iconName);

    private static readonly Dictionary<string, int> s_map = new()
    {
        // Navigation & UI
        ["home"] = 0xE88A,
        ["search"] = 0xE8B6,
        ["settings"] = 0xE8B8,
        ["close"] = 0xE5CD,
        ["menu"] = 0xE5D2,
        ["arrow_back"] = 0xE5C4,
        ["arrow_forward"] = 0xE5C8,
        ["chevron_left"] = 0xE5CB,
        ["chevron_right"] = 0xE5CC,
        ["expand_more"] = 0xE5CF,
        ["expand_less"] = 0xE5CE,
        ["more_vert"] = 0xE5D4,
        ["more_horiz"] = 0xE5D3,
        ["add"] = 0xE145,
        ["remove"] = 0xE15B,
        ["check"] = 0xE5CA,
        ["check_circle"] = 0xE86C,
        ["error"] = 0xE000,
        ["warning"] = 0xE002,
        ["warning_amber"] = 0xE002,
        ["info"] = 0xE88E,
        ["help"] = 0xE887,
        ["favorite"] = 0xE87D,
        ["star"] = 0xE838,

        // Dashboard
        ["dashboard"] = 0xE871,
        ["business"] = 0xE0AF,
        ["group"] = 0xE7EF,
        ["person"] = 0xE7FD,
        ["extension"] = 0xE87B,
        ["widgets"] = 0xE1BD,
        ["build"] = 0xE869,
        ["lock"] = 0xE897,
        ["storage"] = 0xE1DB,
        ["computer"] = 0xE31E,
        ["schedule"] = 0xE8B5,
        ["hourglass_empty"] = 0xE88B,
        ["search_off"] = 0xEA76,

        // Files
        ["folder"] = 0xE2C7,
        ["folder_open"] = 0xE2C8,
        ["folder_shared"] = 0xE2C9,
        ["folder_zip"] = 0xE2C4,
        ["description"] = 0xE873,
        ["image"] = 0xE3F4,
        ["photo_library"] = 0xE413,
        ["picture_as_pdf"] = 0xE415,
        ["text_snippet"] = 0xE8B2,
        ["article"] = 0xEF42,
        ["table_chart"] = 0xE265,
        ["slideshow"] = 0xE41B,
        ["file_upload"] = 0xE2C6,
        ["upload"] = 0xE2C6,
        ["download"] = 0xE2C0,
        ["attach_file"] = 0xE226,

        // Chat & Communication
        ["chat"] = 0xE0B7,
        ["add_comment"] = 0xE266,
        ["send"] = 0xE163,
        ["emoji_emotions"] = 0xEA22,
        ["reply"] = 0xE15E,
        ["forward"] = 0xE154,
        ["block"] = 0xE14B,

        // Call controls
        ["mic"] = 0xE029,
        ["mic_off"] = 0xE02B,
        ["videocam"] = 0xE04B,
        ["videocam_off"] = 0xE04C,
        ["blur_on"] = 0xE3A5,
        ["wallpaper"] = 0xE1BC,
        ["present_to_all"] = 0xE0DF,

        // Media
        ["music_note"] = 0xE405,
        ["movie"] = 0xE02C,
        ["live_tv"] = 0xE639,
        ["play_arrow"] = 0xE037,
        ["pause"] = 0xE034,
        ["skip_next"] = 0xE044,
        ["skip_previous"] = 0xE045,

        // Actions
        ["edit"] = 0xE3C9,
        ["delete"] = 0xE872,
        ["save"] = 0xE161,
        ["sync"] = 0xE627,
        ["link"] = 0xE157,
        ["format_quote"] = 0xE244,
        ["checklist"] = 0xE9B1,
        ["content_copy"] = 0xE14D,

        // Email
        ["email"] = 0xE0BE,
        ["move_to_inbox"] = 0xE168,
        ["outbox"] = 0xE1BF,
        ["inventory_2"] = 0xE1A1,

        // Calendar
        ["calendar_today"] = 0xE935,
        ["calendar_month"] = 0xEBCC,

        // Module-specific
        ["edit_note"] = 0xE745,
        ["assignment"] = 0xE85D,
        ["bookmark"] = 0xE866,
        ["key"] = 0xE73C,
        ["smart_toy"] = 0xEA06,

        // Photos
        ["camera_alt"] = 0xE3B0,
        ["location_on"] = 0xE55A,

        // Other
        ["sprint"] = 0xE889, // rough match
        ["label"] = 0xE892,
        ["handshake"] = 0xEBCB,
        ["history_edu"] = 0xEA3E,
        ["fiber_new"] = 0xE05E,
        ["arrow_drop_down"] = 0xE5C5,
        ["person_add"] = 0xE7FE,
        ["map"] = 0xE55B,
        ["trending_up"] = 0xE8E5,
    };
}
