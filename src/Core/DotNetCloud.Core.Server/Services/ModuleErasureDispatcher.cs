using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.Clients;
using Microsoft.Extensions.Logging;
using Lifecycle = DotNetCloud.Core.Grpc.Lifecycle;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Delivers one <c>EraseUserData</c> call to a module.
/// </summary>
/// <remarks>
/// The erasure orchestrator owns retries, windows and auditing; this seam owns only the transport.
/// Keeping them apart is what makes the orchestration testable without a live module process.
/// </remarks>
internal interface IModuleErasureDispatcher
{
    /// <summary>Calls <c>EraseUserData</c> on a module.</summary>
    /// <param name="moduleId">The module to call.</param>
    /// <param name="requestId">The erasure request id (also the module-side idempotency key).</param>
    /// <param name="userId">The deleted user.</param>
    /// <param name="scopes">The consent scopes the module held (advisory).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The module's answer, or a failure result when it could not be reached.</returns>
    Task<ModuleUserErasureResult> EraseAsync(
        string moduleId,
        Guid requestId,
        Guid userId,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
internal sealed class GrpcModuleErasureDispatcher : IModuleErasureDispatcher
{
    /// <summary>Per-module call deadline. A module that has not answered by then is retried.</summary>
    internal static readonly TimeSpan PerModuleTimeout = TimeSpan.FromSeconds(30);

    private readonly ModuleEndpointProvider _endpoints;
    private readonly ILogger<GrpcModuleErasureDispatcher> _logger;

    public GrpcModuleErasureDispatcher(
        ModuleEndpointProvider endpoints,
        ILogger<GrpcModuleErasureDispatcher> logger)
    {
        _endpoints = endpoints;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ModuleUserErasureResult> EraseAsync(
        string moduleId,
        Guid requestId,
        Guid userId,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var channel = _endpoints.GetChannel(moduleId);
            var client = new Lifecycle.ModuleLifecycle.ModuleLifecycleClient(channel);

            var response = await client.EraseUserDataAsync(
                new Lifecycle.EraseUserDataRequest
                {
                    RequestId = requestId.ToString(),
                    UserId = userId.ToString(),
                    Scopes = { scopes },
                },
                deadline: DateTime.UtcNow.Add(PerModuleTimeout),
                cancellationToken: cancellationToken);

            return new ModuleUserErasureResult(
                response.Acknowledged,
                response.DeletedCounts,
                string.IsNullOrWhiteSpace(response.Error) ? null : response.Error,
                response.NotApplicable);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "User erasure {RequestId} delivery to module {ModuleId} failed: {Error}",
                requestId,
                LogSanitizer.Sanitize(moduleId),
                LogSanitizer.Sanitize(ex.Message));

            return ModuleUserErasureResult.Failed(ex.Message);
        }
    }
}
