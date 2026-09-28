namespace DotNetCloud.Client.Android.Chat;

/// <summary>
/// The message and attachment limits an administrator configured for the Chat module, as reported by
/// <c>GET /api/v1/chat/limits</c>. Clients apply them to their own entry controls so an over-limit
/// send never has to fail server-side (the server still enforces them as the safety net).
/// </summary>
/// <param name="MaxMessageLength">Characters allowed per message; <c>0</c> means unlimited.</param>
/// <param name="MaxAttachmentsPerMessage">Attachments allowed on one message; <c>0</c> means unlimited.</param>
/// <param name="MaxAttachmentSizeMb">Size limit of a single attachment in MB; <c>0</c> means unlimited.</param>
/// <remarks>
/// A value of zero is only reachable if the server does not report limits at all — the Chat module
/// itself clamps the message length to 1–10000 and the attachment size to 1–64 MB.
/// </remarks>
public sealed record ChatLimits(int MaxMessageLength, int MaxAttachmentsPerMessage, int MaxAttachmentSizeMb)
{
    /// <summary>
    /// Built-in defaults, used when the server does not report limits (an older module host, an
    /// unreachable endpoint, or a <c>503 CHAT_SETTINGS_UNAVAILABLE</c>). They equal the server's own
    /// permissive defaults, so falling back to them never blocks a message the server would accept.
    /// </summary>
    public static ChatLimits Defaults { get; } = new(10000, 10, 10);

    /// <summary>Whether a message-length limit is configured and should be enforced by the composer.</summary>
    public bool HasMessageLengthLimit => MaxMessageLength > 0;

    /// <summary>Whether an attachment-size limit is configured and should be enforced before uploading.</summary>
    public bool HasAttachmentSizeLimit => MaxAttachmentSizeMb > 0;

    /// <summary>Maximum size of a single attachment in bytes; <c>0</c> when unlimited.</summary>
    public long MaxAttachmentBytes => MaxAttachmentSizeMb > 0 ? MaxAttachmentSizeMb * 1024L * 1024L : 0L;

    /// <summary>Returns whether <paramref name="text"/> is longer than the configured message limit.</summary>
    /// <param name="text">Text to test; <see langword="null"/> counts as empty.</param>
    /// <returns><see langword="true"/> when the text exceeds the configured limit.</returns>
    public bool IsOverMessageLength(string? text) =>
        MaxMessageLength > 0 && (text?.Length ?? 0) > MaxMessageLength;

    /// <summary>
    /// Truncates <paramref name="text"/> to the configured message limit. Text at or below the limit
    /// is returned unchanged, including when no limit is configured.
    /// </summary>
    /// <param name="text">Text to clamp; <see langword="null"/> is treated as empty.</param>
    /// <returns>The text, shortened to the limit when it was longer.</returns>
    public string ClampMessageText(string? text)
    {
        var value = text ?? string.Empty;
        if (!IsOverMessageLength(value))
            return value;

        var length = MaxMessageLength;

        // Never leave half of a surrogate pair behind: an emoji typed or picked at the boundary would
        // otherwise be cut in two and render as a replacement character.
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
            length--;

        return value[..length];
    }
}
