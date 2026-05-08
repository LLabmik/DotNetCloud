using DotNetCloud.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.About;

/// <summary>
/// About module — displays technical information, version details, and open-source attributions.
/// This module has no database; it is purely display-driven.
/// </summary>
public sealed class AboutModule : IModuleLifecycle
{
    private ILogger<AboutModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new AboutModuleManifest();

    /// <inheritdoc />
    public Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<AboutModule>>();
        _logger?.LogInformation("Initializing About module ({ModuleId})", context.ModuleId);

        _initialized = true;
        _logger?.LogInformation("About module initialized successfully");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            throw new InvalidOperationException("Module must be initialized before starting.");

        _running = true;
        _logger?.LogInformation("About module started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;
        _logger?.LogInformation("About module stopped");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("About module disposed");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

    /// <summary>Gets whether the module has been initialized.</summary>
    public bool IsInitialized => _initialized;

    /// <summary>Gets whether the module is currently running.</summary>
    public bool IsRunning => _running;
}
