namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Pure decision policy for fast-tracking enrichment of small batches of newly added
/// videos. When a user adds ≤ <see cref="MaxQuickBatchSize"/> videos in quick
/// succession they are enqueued for immediate enrichment; larger bursts are left to
/// the daily enrichment job.
/// </summary>
internal static class QuickVideoEnrichmentPolicy
{
    /// <summary>
    /// Maximum number of newly added videos in a burst that qualifies for fast-track
    /// enrichment. More than this → leave to the daily job (keeps bulk imports and
    /// large scans off the fast path and avoids hammering the TMDB API).
    /// </summary>
    public const int MaxQuickBatchSize = 5;

    /// <summary>
    /// Videos created within this window of "now" are considered recently added
    /// fast-track candidates.
    /// </summary>
    public static readonly TimeSpan LookbackWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// A burst must have no new videos for at least this long before it is evaluated,
    /// so uploads arriving in quick succession are counted as one batch.
    /// </summary>
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How often the fast-track service polls for new bursts.
    /// </summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Returns true when a burst qualifies for fast-track enrichment: between 1 and
    /// <see cref="MaxQuickBatchSize"/> videos and the newest one has been quiet for at
    /// least <see cref="QuietPeriod"/>.
    /// </summary>
    /// <param name="candidateCount">Number of recently added, unenriched videos.</param>
    /// <param name="sinceNewestCandidate">Time elapsed since the newest candidate was created.</param>
    public static bool ShouldFastTrack(int candidateCount, TimeSpan sinceNewestCandidate)
        => candidateCount is > 0 and <= MaxQuickBatchSize
           && sinceNewestCandidate >= QuietPeriod;

    /// <summary>
    /// Returns true when a burst is too large for fast-track enrichment and should be
    /// left for the daily job.
    /// </summary>
    /// <param name="candidateCount">Number of recently added, unenriched videos.</param>
    public static bool ExceedsThreshold(int candidateCount) => candidateCount > MaxQuickBatchSize;
}
