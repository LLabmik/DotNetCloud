using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Test double that serves a fixed <see cref="CollaboraOptions"/> through
/// <see cref="ICollaboraSettingsProvider"/>, so Collabora services can be constructed in tests
/// without a database (the real provider layers admin settings over configuration).
/// </summary>
internal sealed class TestCollaboraSettings : ICollaboraSettingsProvider
{
    private readonly CollaboraOptions _options;

    public TestCollaboraSettings(CollaboraOptions options) => _options = options;

    /// <summary>Wraps configuration-style options.</summary>
    public static TestCollaboraSettings From(IOptions<CollaboraOptions> options) => new(options.Value);

    /// <inheritdoc />
    public CollaboraOptions Current => _options;

    /// <inheritdoc />
    public Task<CollaboraOptions> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_options);

    /// <inheritdoc />
    public void Invalidate()
    {
        // Nothing is cached.
    }
}
