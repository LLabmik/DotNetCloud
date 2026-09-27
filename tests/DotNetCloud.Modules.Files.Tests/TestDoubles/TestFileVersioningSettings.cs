using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Test double that serves a fixed <see cref="VersionRetentionOptions"/> through
/// <see cref="IFileVersioningSettingsProvider"/>, so the services that record versions can be
/// constructed in tests without a database (the real provider layers the admin settings rows over
/// configuration).
/// </summary>
internal sealed class TestFileVersioningSettings : IFileVersioningSettingsProvider
{
    private readonly VersionRetentionOptions _options;

    public TestFileVersioningSettings(VersionRetentionOptions options) => _options = options;

    /// <summary>A provider with the default policy (versioning on, unlimited history).</summary>
    public static TestFileVersioningSettings Default => new(new VersionRetentionOptions { MaxVersionCount = 0 });

    /// <summary>Wraps an explicit policy.</summary>
    public static TestFileVersioningSettings From(VersionRetentionOptions options) => new(options);

    /// <inheritdoc />
    public VersionRetentionOptions Current => _options;

    /// <inheritdoc />
    public Task<VersionRetentionOptions> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_options);

    /// <inheritdoc />
    public void Invalidate()
    {
        // Nothing is cached.
    }
}
