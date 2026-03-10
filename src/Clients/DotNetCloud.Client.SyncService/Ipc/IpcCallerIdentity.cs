using System.Runtime.InteropServices;

namespace DotNetCloud.Client.SyncService.Ipc;

/// <summary>
/// Normalized caller identity resolved from the IPC transport layer.
/// </summary>
public sealed class IpcCallerIdentity
{
    private static readonly StringComparer OwnerComparer =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    /// <summary>Represents an unavailable or unverifiable caller identity.</summary>
    public static IpcCallerIdentity Unavailable { get; } = new(false, null, null, null);

    /// <summary>Initializes a new <see cref="IpcCallerIdentity"/> instance.</summary>
    public IpcCallerIdentity(bool isAvailable, string? rawIdentity, string? normalizedIdentity, string? accountName)
    {
        IsAvailable = isAvailable;
        RawIdentity = rawIdentity;
        NormalizedIdentity = normalizedIdentity;
        AccountName = accountName;
    }

    /// <summary>True when caller identity could be resolved from transport credentials.</summary>
    public bool IsAvailable { get; }

    /// <summary>Original identity string from transport (for diagnostics).</summary>
    public string? RawIdentity { get; }

    /// <summary>Normalized identity used for comparison and logging.</summary>
    public string? NormalizedIdentity { get; }

    /// <summary>Account/username component extracted from identity (for ownership checks).</summary>
    public string? AccountName { get; }

    /// <summary>
    /// Creates a caller identity from a Windows named-pipe impersonation username.
    /// </summary>
    public static IpcCallerIdentity FromWindowsPipeUserName(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return Unavailable;

        var normalized = userName.Trim();
        var account = ExtractAccountName(normalized);
        return new IpcCallerIdentity(true, normalized, normalized, account);
    }

    /// <summary>
    /// Creates a caller identity for Unix socket peer credentials when only username is available.
    /// </summary>
    public static IpcCallerIdentity FromUnixUserName(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return Unavailable;

        var normalized = userName.Trim();
        return new IpcCallerIdentity(true, normalized, normalized, normalized);
    }

    /// <summary>Returns true if this caller is allowed to access contexts owned by <paramref name="ownerUserName"/>.</summary>
    public bool MatchesOwner(string ownerUserName)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(ownerUserName))
            return false;

        var owner = ownerUserName.Trim();
        if (AccountName is not null && OwnerComparer.Equals(AccountName, owner))
            return true;

        return NormalizedIdentity is not null && OwnerComparer.Equals(NormalizedIdentity, owner);
    }

    private static string ExtractAccountName(string identity)
    {
        var trimmed = identity.Trim();
        var slashIdx = trimmed.LastIndexOf('\\');
        if (slashIdx >= 0 && slashIdx < trimmed.Length - 1)
            return trimmed[(slashIdx + 1)..];

        var atIdx = trimmed.IndexOf('@');
        if (atIdx > 0)
            return trimmed[..atIdx];

        return trimmed;
    }
}