#if WINDOWS_BUILD
using System.Management;
#endif
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace DotNetCloud.Client.Core.Platform;

/// <summary>
/// Windows Volume Shadow Copy Service (VSS) implementation of <see cref="ILockedFileReader"/>.
/// Creates one shadow copy per sync pass on first need, reads all locked files from it,
/// and releases the snapshot at the end of the pass via <see cref="ReleaseSnapshot"/>.
/// </summary>
/// <remarks>
/// VSS snapshot creation requires elevated privileges (SYSTEM account or Backup Operators).
/// The DotNetCloud sync service runs as a Windows Service under SYSTEM, satisfying this requirement.
/// Falls back gracefully (returns <see langword="null"/>) when snapshot creation fails.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class VssLockedFileReader : ILockedFileReader
{
    private readonly ILogger<VssLockedFileReader> _logger;
    private readonly SemaphoreSlim _snapshotLock = new(1, 1);

#if WINDOWS_BUILD
    private string? _shadowDeviceName;
    private string? _shadowedVolumeRoot;
    private string? _shadowCopyId;
#endif

    /// <summary>Initializes a new <see cref="VssLockedFileReader"/>.</summary>
    public VssLockedFileReader(ILogger<VssLockedFileReader> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Stream?> TryReadLockedFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return Task.FromResult<Stream?>(null);
#if WINDOWS_BUILD
        return TryReadLockedFileWindowsAsync(path, cancellationToken);
#else
        return Task.FromResult<Stream?>(null);
#endif
    }

#if WINDOWS_BUILD
    [SupportedOSPlatform("windows")]
    private async Task<Stream?> TryReadLockedFileWindowsAsync(string path, CancellationToken cancellationToken)
    {
        await _snapshotLock.WaitAsync(cancellationToken);
        try
        {
            var volumeRoot = Path.GetPathRoot(path)!;

            // Create snapshot on first call, or if this file is on a different volume.
            if (_shadowDeviceName is null ||
                !string.Equals(volumeRoot, _shadowedVolumeRoot, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryCreateSnapshot(volumeRoot))
                    return null;
            }

            var relativePath = path.Substring(volumeRoot.Length);
            var shadowPath = _shadowDeviceName + Path.DirectorySeparatorChar + relativePath;

            if (!File.Exists(shadowPath))
            {
                _logger.LogDebug("Shadow copy path {ShadowPath} not found for {Path}.", shadowPath, path);
                return null;
            }

            return new FileStream(shadowPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VSS read failed for {Path}.", path);
            return null;
        }
        finally
        {
            _snapshotLock.Release();
        }
    }

    [SupportedOSPlatform("windows")]
    private bool TryCreateSnapshot(string volumeRoot)
    {
        try
        {
            using var shadowCopyClass = new ManagementClass(@"\\.\root\cimv2", "Win32_ShadowCopy", null);
            using var inParams = shadowCopyClass.GetMethodParameters("Create");
            inParams["Volume"] = volumeRoot;
            inParams["Context"] = "ClientAccessible";

            using var outParams = shadowCopyClass.InvokeMethod("Create", inParams, null);
            var returnValue = (uint)(outParams["ReturnValue"] ?? 1u);
            if (returnValue != 0)
            {
                _logger.LogWarning(
                    "Win32_ShadowCopy.Create returned {RetVal} for volume {Volume}.",
                    returnValue, volumeRoot);
                return false;
            }

            var shadowId = (string?)outParams["ShadowID"];
            if (string.IsNullOrWhiteSpace(shadowId))
                return false;

            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\cimv2",
                $"SELECT DeviceObject FROM Win32_ShadowCopy WHERE ID = '{shadowId}'");

            foreach (ManagementObject shadow in searcher.Get())
            {
                _shadowCopyId = shadowId;
                _shadowDeviceName = (string?)shadow["DeviceObject"];
                _shadowedVolumeRoot = volumeRoot;
                _logger.LogInformation(
                    "VSS shadow copy created for {Volume}: {Device}.",
                    volumeRoot, _shadowDeviceName);
                return _shadowDeviceName is not null;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create VSS shadow copy for {Volume}.", volumeRoot);
            ReleaseSnapshot();
            return false;
        }
    }
#endif

    /// <inheritdoc/>
    public void ReleaseSnapshot()
    {
#if WINDOWS_BUILD
        var id = _shadowCopyId;
        _shadowCopyId = null;
        _shadowDeviceName = null;
        _shadowedVolumeRoot = null;
        if (id is null) return;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\cimv2",
                $"SELECT * FROM Win32_ShadowCopy WHERE ID = '{id}'");
            foreach (ManagementObject shadow in searcher.Get())
            {
                shadow.InvokeMethod("Delete", null);
                _logger.LogDebug("Released VSS shadow copy {Id}.", id);
                break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete VSS shadow copy {Id}.", id);
        }
#endif
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        ReleaseSnapshot();
        _snapshotLock.Dispose();
    }
}
