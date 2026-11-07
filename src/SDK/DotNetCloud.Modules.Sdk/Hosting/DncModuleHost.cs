using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Sdk.Hosting;

/// <summary>
/// The lifecycle a sandboxed module host implements.
/// </summary>
/// <remarks>
/// Mirrors <see cref="IModule"/> from the core abstractions: the processor initializes, starts and
/// stops the module, and a module must be safe to stop even if start failed part-way.
/// </remarks>
public interface IDncModuleLifecycle
{
    /// <summary>Prepares the module (configuration, clients) without serving traffic.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when initialization finished.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts serving.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the module is serving.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops serving and releases resources. Must be safe to call at any time.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the module stopped.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Base class for a module host: implements the lifecycle contract with state tracking so a stop is
/// always safe and a start cannot be duplicated.
/// </summary>
public abstract class DncModuleHost : IDncModuleLifecycle, IAsyncDisposable
{
    /// <summary>Gets the module's runtime environment.</summary>
    protected ModuleEnvironment Environment { get; private set; } = null!;

    /// <summary>Gets a value indicating whether the module completed its start.</summary>
    public bool IsStarted { get; private set; }

    /// <summary>
    /// Initializes the host with the environment the supervisor provided.
    /// </summary>
    /// <param name="environment">The module environment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the module initialized.</returns>
    public async Task InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);

        Environment = environment;
        await InitializeAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Environment is null)
        {
            throw new InvalidOperationException(
                "The host has no environment. Call InitializeAsync(ModuleEnvironment) or construct the host with one.");
        }

        await OnInitializeAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Environment is null)
        {
            throw new InvalidOperationException(
                "The host has no environment. Call InitializeAsync(ModuleEnvironment) before starting the module.");
        }

        if (IsStarted)
        {
            return;
        }

        await OnStartAsync(cancellationToken);
        IsStarted = true;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsStarted)
        {
            return;
        }

        try
        {
            await OnStopAsync(cancellationToken);
        }
        finally
        {
            IsStarted = false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>Initializes the module. Override instead of <see cref="InitializeAsync(CancellationToken)"/>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when initialization finished.</returns>
    protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Starts the module. Override instead of <see cref="StartAsync"/>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the module is serving.</returns>
    protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Stops the module. Override instead of <see cref="StopAsync"/>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the module stopped.</returns>
    protected virtual Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
