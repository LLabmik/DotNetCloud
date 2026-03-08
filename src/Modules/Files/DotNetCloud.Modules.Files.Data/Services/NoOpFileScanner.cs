using DotNetCloud.Modules.Files.Services;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// No-operation implementation of <see cref="IFileScanner"/>.
/// Always reports files as clean. Used until a real scanner (e.g., ClamAV) is integrated.
/// </summary>
public sealed class NoOpFileScanner : IFileScanner
{
    private static readonly ScanResult CleanResult = new(IsClean: true, ScannerName: "NoOp");

    /// <inheritdoc />
    public Task<ScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
        => Task.FromResult(CleanResult);
}
