using DotNetCloud.Core.Modules;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.MyModule;

/// <summary>
/// The module itself. The supervisor drives this through the lifecycle gRPC service: Initialize,
/// Start, Stop, then Dispose when the process ends.
/// </summary>
/// <remarks>
/// This skeleton holds no per-user data, so it does not register an erasure handler and
/// <c>manifest.json</c> declares <c>dataStore: "none"</c>. Adding a store means: a
/// <c>DbContext</c> with its own migrations, <c>dataStore: "isolated"</c>,
/// <c>handlesUserErasure: true</c>, and an <c>IModuleUserErasureHandler</c> — see the Example module.
/// </remarks>
public sealed class MyModule : IModuleLifecycle
{
    private ILogger<MyModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new MyModuleManifest();

    /// <summary>Gets a value indicating whether the core has started the module.</summary>
    public bool IsRunning => _running;

    /// <inheritdoc />
    public Task InitializeAsync(
        ModuleInitializationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<MyModule>>();
        _logger?.LogInformation("Initializing {ModuleId}", context.ModuleId);

        _initialized = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("The module must be initialized before it is started.");
        }

        _running = true;
        _logger?.LogInformation("Started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;
        _logger?.LogInformation("Stopped");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("Disposed");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());
}
