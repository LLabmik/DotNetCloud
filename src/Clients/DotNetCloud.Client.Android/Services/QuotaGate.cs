namespace DotNetCloud.Client.Android.Services;

/// <summary>Outcome of evaluating the user's storage quota before an upload pass.</summary>
public enum UploadQuotaState
{
    /// <summary>No finite quota (server reports 0 / unlimited) — uploads are never quota-blocked.</summary>
    NotLimited,

    /// <summary>A finite quota exists and there is room for the upload.</summary>
    Ok,

    /// <summary>A finite quota exists and it is exhausted (or the file does not fit) — uploads would be rejected.</summary>
    Full
}

/// <summary>
/// Pure quota math used by the media auto-upload watcher so it can stop before the server
/// rejects an upload with <c>409 / FILES_QUOTA_EXCEEDED</c> and notify the user.
/// Semantics match the server: <c>MaxBytes == 0</c> means unlimited storage.
/// </summary>
internal static class QuotaGate
{
    /// <summary>
    /// Bytes of remaining quota. Returns <see cref="long.MaxValue"/> when the quota is
    /// unlimited (total ≤ 0), mirroring the server's <c>FileQuota.RemainingBytes</c>.
    /// </summary>
    public static long RemainingBytes(long totalBytes, long usedBytes)
        => totalBytes > 0 ? Math.Max(0, totalBytes - usedBytes) : long.MaxValue;

    /// <summary>Returns <c>true</c> when the account has a finite quota to respect.</summary>
    public static bool HasFiniteQuota(long totalBytes) => totalBytes > 0;

    /// <summary>
    /// Classifies an account state. When <paramref name="requiredBytes"/> is provided the
    /// result is <see cref="UploadQuotaState.Full"/> whenever the requested size does not fit
    /// in the remaining quota; otherwise it is <see cref="UploadQuotaState.Full"/> only when
    /// the quota is fully exhausted.
    /// </summary>
    public static UploadQuotaState Evaluate(long totalBytes, long usedBytes, long? requiredBytes = null)
    {
        if (totalBytes <= 0)
            return UploadQuotaState.NotLimited;

        var remaining = Math.Max(0, totalBytes - usedBytes);
        if (requiredBytes.HasValue)
            return remaining >= requiredBytes.Value ? UploadQuotaState.Ok : UploadQuotaState.Full;
        return remaining > 0 ? UploadQuotaState.Ok : UploadQuotaState.Full;
    }
}
