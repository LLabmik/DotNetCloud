namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>Result of an immediate provisioning call for one user.</summary>
/// <param name="Success">Whether the pass completed without a fatal error.</param>
/// <param name="Status">Provisioning status name.</param>
/// <param name="ItemsCreated">Items added.</param>
/// <param name="ItemsSkipped">Items skipped.</param>
/// <param name="BytesCopied">Bytes provisioned.</param>
/// <param name="ErrorMessage">Failure reason, if any.</param>
public sealed record ProvisionUserDefaultsResult(
    bool Success,
    string Status,
    int ItemsCreated,
    int ItemsSkipped,
    long BytesCopied,
    string? ErrorMessage);

/// <summary>Result of a batch enqueue call.</summary>
/// <param name="Queued">Users queued.</param>
/// <param name="Skipped">Users already queued or running.</param>
public sealed record EnqueueProvisioningResult(int Queued, int Skipped);
