using Android.App;
using Android.Content;
using Android.OS;
using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android;

/// <summary>An installed UnifiedPush distributor.</summary>
/// <param name="PackageName">Package name.</param>
/// <param name="Label">Display name, when it could be resolved.</param>
internal sealed record DistributorCandidate(string PackageName, string? Label);

/// <summary>
/// Turns the Android-free <see cref="UnifiedPushIntentDescriptor"/> values produced by
/// <see cref="UnifiedPushProtocol"/> into real broadcasts, and discovers installed distributors.
/// </summary>
internal static class UnifiedPushIntents
{
    /// <summary>Converts a descriptor into an <see cref="Intent"/> with its string extras.</summary>
    /// <param name="descriptor">Descriptor to convert.</param>
    /// <returns>The intent.</returns>
    public static Intent ToAndroidIntent(UnifiedPushIntentDescriptor descriptor)
    {
        var intent = new Intent(descriptor.Action);
        foreach (var (key, value) in descriptor.Extras)
            intent.PutExtra(key, value);

        return intent;
    }

    /// <summary>
    /// Sends a connector → distributor broadcast.
    /// </summary>
    /// <param name="context">Context used to send the broadcast.</param>
    /// <param name="descriptor">Descriptor to send; null is a no-op.</param>
    /// <param name="targetPackage">Distributor to target, or null to broadcast to all of them.</param>
    /// <returns>True when the broadcast was handed to the system.</returns>
    public static bool Send(Context? context, UnifiedPushIntentDescriptor? descriptor, string? targetPackage = null)
    {
        if (context is null || descriptor is null)
            return false;

        try
        {
            var intent = ToAndroidIntent(descriptor);
            if (!string.IsNullOrWhiteSpace(targetPackage))
                intent.SetPackage(targetPackage);

            var options = descriptor.ShareIdentity ? BuildShareIdentityOptions() : null;
            if (options is not null && OperatingSystem.IsAndroidVersionAtLeast(34))
            {
#pragma warning disable CA1416 // The share-identity overload requires API 34 — guarded above
                // null! for the receiver permission: the Java API documents null as "no
                // permission required", but the binding annotates the parameter non-nullable.
                context.SendBroadcast(intent, null!, options);
#pragma warning restore CA1416
            }
            else
            {
                context.SendBroadcast(intent);
            }

            return true;
        }
        catch (Exception ex)
        {
            // Broadcasts from background components must never crash the app, but a silent failure
            // makes a missing acknowledgement impossible to diagnose.
            global::Android.Util.Log.Warn(
                "DotNetCloud", $"[UnifiedPushIntents] Sending {descriptor.Action} failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Lists installed distributors (apps exposing the UnifiedPush registration receiver).
    /// </summary>
    /// <param name="context">Context used to query the package manager.</param>
    /// <returns>Distinct distributors; empty when none is installed.</returns>
    public static IReadOnlyList<DistributorCandidate> FindDistributors(Context? context)
    {
        var packageManager = context?.PackageManager;
        if (packageManager is null)
            return [];

        try
        {
            var probe = new Intent(UnifiedPushProtocol.ActionRegister);
#pragma warning disable CA1416 // QueryBroadcastReceivers(Intent, PackageInfoFlags) maps to the API-1 method
            var receivers = packageManager.QueryBroadcastReceivers(
                probe, global::Android.Content.PM.PackageInfoFlags.MatchAll);
#pragma warning restore CA1416

            if (receivers is null)
                return [];

            var candidates = new List<DistributorCandidate>();
            foreach (var receiver in receivers)
            {
                var packageName = receiver.ActivityInfo?.PackageName;
                if (string.IsNullOrWhiteSpace(packageName)
                    || candidates.Any(c => string.Equals(c.PackageName, packageName, StringComparison.Ordinal)))
                {
                    continue;
                }

                candidates.Add(new DistributorCandidate(packageName, TryReadLabel(receiver, packageManager)));
            }

            return candidates;
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>Resolves a distributor's display name for the Settings card.</summary>
    /// <param name="context">Context used to query the package manager.</param>
    /// <param name="packageName">Distributor package name.</param>
    /// <returns>The label, or null when it cannot be resolved.</returns>
    public static string? TryGetDistributorLabel(Context? context, string? packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return null;

        return FindDistributors(context)
            .FirstOrDefault(candidate => string.Equals(
                candidate.PackageName, packageName, StringComparison.Ordinal))
            ?.Label;
    }

    private static string? TryReadLabel(global::Android.Content.PM.ResolveInfo receiver, global::Android.Content.PM.PackageManager packageManager)
    {
        try
        {
            return receiver.LoadLabel(packageManager)?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    // targetSdk >= 34 identifies the sending app with FLAG_SHARE_IDENTITY instead of a
    // PendingIntent, which is what the specification requires for REGISTER/UNREGISTER.
    private static Bundle? BuildShareIdentityOptions()
    {
#pragma warning disable CA1416 // BroadcastOptions share-identity requires API 34 — guarded below
        if (!OperatingSystem.IsAndroidVersionAtLeast(34))
            return null;

        var options = BroadcastOptions.MakeBasic();
        if (options is null)
            return null;

        options.SetShareIdentityEnabled(true);
        return options.ToBundle();
#pragma warning restore CA1416
    }
}
