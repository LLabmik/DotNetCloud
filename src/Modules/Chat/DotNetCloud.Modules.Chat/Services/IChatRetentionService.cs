namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Summary of a single retention/archiving sweep.
/// </summary>
public sealed record ChatRetentionSweepResult
{
    /// <summary>Number of channels examined.</summary>
    public int ChannelsScanned { get; init; }

    /// <summary>
    /// Number of messages written to the archive directory and then removed from the database
    /// (Archive mode).</summary>
    public int MessagesArchived { get; init; }

    /// <summary>Number of messages permanently deleted because they passed the retention policy (Purge mode).</summary>
    public int MessagesPurged { get; init; }

    /// <summary>Number of attachment rows removed along with their message.</summary>
    public int AttachmentsRemoved { get; init; }

    /// <summary>Number of attachment payloads copied into the archive directory (Archive mode).</summary>
    public int AttachmentsArchived { get; init; }

    /// <summary>
    /// Messages that were <b>kept</b> because their archive export failed. Non-zero only in
    /// Archive mode, where a message is deleted only after it has been written to disk; they are
    /// retried on the next sweep.
    /// </summary>
    public int MessagesSkipped { get; init; }

    /// <summary>Number of pins removed because their message expired.</summary>
    public int PinsRemoved { get; init; }

    /// <summary>Whether the sweep did nothing because the retention policy is disabled.</summary>
    public bool Skipped { get; init; }

    /// <summary>UTC time the sweep finished.</summary>
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Applies the configured retention policy to chat messages and their attachments.
/// </summary>
public interface IChatRetentionService
{
    /// <summary>
    /// Runs one sweep across every channel: messages that exceed
    /// <see cref="ChatSettings.MaxMessagesPerChannel"/> or are older than
    /// <see cref="ChatSettings.MessageLifetimeDays"/> are archived or purged
    /// according to <see cref="ChatSettings.RetentionMode"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary of the work performed.</returns>
    Task<ChatRetentionSweepResult> SweepAsync(CancellationToken cancellationToken = default);
}
