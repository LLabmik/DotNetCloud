using System.Collections.Concurrent;
using DotNetCloud.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Resolves the user-erasure handler of the module running in this process.
/// </summary>
/// <remarks>
/// <para>
/// The lifecycle service base class cannot take constructor dependencies — every host derives from
/// it with its own parameterless-shaped service — so the handler is published here by DI:
/// <see cref="ModuleUserErasureServiceCollectionExtensions.AddModuleUserErasureHandler{THandler}(IServiceCollection, string?)"/>
/// registers the handler and a bootstrapper that materializes it at startup, which records it
/// against this process's <c>DOTNETCLOUD_MODULE_ID</c>.
/// </para>
/// <para>
/// A process that never registers a handler is treated as a module that holds no per-user data,
/// which is the honest default: <c>EraseUserData</c> answers <c>not_applicable</c>.
/// </para>
/// </remarks>
public static class ModuleUserErasureHandlers
{
    private static readonly ConcurrentDictionary<string, IModuleUserErasureHandler> Handlers =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers (or replaces) the handler for a module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="handler">The handler.</param>
    public static void Register(string moduleId, IModuleUserErasureHandler handler)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(handler);
        Handlers[moduleId] = handler;
    }

    /// <summary>Resolves the handler for a module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The handler, or <see langword="null"/> when the module registered none.</returns>
    public static IModuleUserErasureHandler? Resolve(string? moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && Handlers.TryGetValue(moduleId, out var handler)
            ? handler
            : null;

    /// <summary>Clears the registry (tests only).</summary>
    internal static void Clear() => Handlers.Clear();
}

/// <summary>
/// Registers a module host's user-erasure handler.
/// </summary>
public static class ModuleUserErasureServiceCollectionExtensions
{
    /// <summary>
    /// Adds <typeparamref name="THandler"/> as this process's user-erasure handler.
    /// </summary>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="moduleId">
    /// The module id; defaults to the <c>DOTNETCLOUD_MODULE_ID</c> environment variable supplied by
    /// the supervisor.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModuleUserErasureHandler<THandler>(
        this IServiceCollection services,
        string? moduleId = null)
        where THandler : class, IModuleUserErasureHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        var resolvedModuleId = moduleId
            ?? Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID")
            ?? string.Empty;

        services.AddSingleton<IModuleUserErasureHandler>(provider =>
        {
            var handler = ActivatorUtilities.CreateInstance<THandler>(provider);
            ModuleUserErasureHandlers.Register(resolvedModuleId, handler);
            return handler;
        });

        services.AddHostedService<ModuleUserErasureHandlerBootstrapper>();

        return services;
    }
}

/// <summary>
/// Materializes the registered user-erasure handler at startup so it is available before the first
/// <c>EraseUserData</c> call arrives.
/// </summary>
internal sealed class ModuleUserErasureHandlerBootstrapper : IHostedService
{
    private readonly IModuleUserErasureHandler _handler;
    private readonly ILogger<ModuleUserErasureHandlerBootstrapper> _logger;

    public ModuleUserErasureHandlerBootstrapper(
        IModuleUserErasureHandler handler,
        ILogger<ModuleUserErasureHandlerBootstrapper> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("User-erasure handler {Handler} is ready.", _handler.GetType().Name);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
