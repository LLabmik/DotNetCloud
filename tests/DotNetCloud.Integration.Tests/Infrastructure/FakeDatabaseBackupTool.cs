using DotNetCloud.Core.Server.Services;

namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// In-test <see cref="IDatabaseBackupTool"/> that writes a placeholder dump instead of running
/// <c>pg_dump</c>/<c>sqlcmd</c>, so backup and restore endpoints can be exercised on a machine
/// without the provider-native tools installed.
/// </summary>
internal sealed class FakeDatabaseBackupTool : IDatabaseBackupTool
{
    /// <summary>
    /// Gets the dump contents written by <see cref="CreateDumpAsync"/>.
    /// </summary>
    public const string DumpContents = "-- dotnetcloud integration test dump\n";

    /// <summary>
    /// Gets the providers passed to <see cref="CreateDumpAsync"/>.
    /// </summary>
    public List<string> DumpedProviders { get; } = [];

    /// <summary>
    /// Gets the dump files passed to <see cref="RestoreDumpAsync"/>.
    /// </summary>
    public List<string> RestoredDumps { get; } = [];

    /// <inheritdoc />
    public async Task CreateDumpAsync(
        string provider,
        string? connectionString,
        string outputPath,
        CancellationToken cancellationToken)
    {
        DumpedProviders.Add(provider);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(outputPath, DumpContents, cancellationToken);
    }

    /// <inheritdoc />
    public Task RestoreDumpAsync(
        string provider,
        string? connectionString,
        string dumpFilePath,
        CancellationToken cancellationToken)
    {
        RestoredDumps.Add(dumpFilePath);
        return Task.CompletedTask;
    }
}
