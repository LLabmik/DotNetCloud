namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Payload describing an image pasted into the chat composer.
/// </summary>
public sealed class PastedImageData
{
    /// <summary>Suggested file name for upload.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>MIME content type (for example, image/png).</summary>
    public string ContentType { get; init; } = "image/png";

    /// <summary>Raw image bytes extracted from clipboard data.</summary>
    public byte[] Data { get; init; } = [];

    /// <summary>Reported payload size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Server URL when already uploaded via HTTP (null when using SignalR fallback).</summary>
    public string? Url { get; init; }
}