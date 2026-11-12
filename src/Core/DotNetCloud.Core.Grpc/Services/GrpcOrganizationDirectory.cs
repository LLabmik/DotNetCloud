using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc.Services;

/// <summary>
/// gRPC-based implementation of <see cref="IOrganizationDirectory"/> for use in
/// process-isolated module hosts.
/// </summary>
/// <remarks>
/// <para>
/// Connects to Core.Server over the internal gRPC channel identified by the
/// <c>DOTNETCLOUD_CORE_ENDPOINT</c> environment variable (set by <c>ProcessSupervisor</c> when it
/// launches a module host), sending the <c>module-id</c> metadata header so the core can attribute
/// the call. The module has no database access to <c>core.organizations</c>, so this client is the
/// only way a sandboxed module can discover which organizations its acting user belongs to.
/// </para>
/// <para>
/// When <c>DOTNETCLOUD_CORE_ENDPOINT</c> is absent (manual host run, unit tests, host started
/// outside the supervisor) the client degrades gracefully: the listing returns empty and a warning
/// is logged. An organization lookup must never crash module startup or a business operation — the
/// failure mode is "no organizations", which a UI renders as an empty picker rather than an error.
/// </para>
/// <para>
/// Only <see cref="GetUserOrganizationsAsync"/> is exposed over gRPC today.
/// <see cref="IsOrganizationMemberAsync"/> is answered from that listing (fail-closed: an
/// unreachable core reports "not a member"). Role-level queries return conservative defaults and log
/// once per call at warning level, because a silent <c>false</c> on an admin check is the kind of
/// default that hides a broken deployment.
/// </para>
/// </remarks>
public sealed class GrpcOrganizationDirectory : IOrganizationDirectory, IDisposable
{
    private readonly ILogger<GrpcOrganizationDirectory> _logger;
    private readonly string? _coreEndpoint;
    private readonly string _moduleId;
    private readonly object _sync = new();
    private GrpcChannel? _channel;
    private CoreCapabilities.CoreCapabilitiesClient? _client;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcOrganizationDirectory"/> class.
    /// </summary>
    /// <param name="logger">The logger for this client.</param>
    public GrpcOrganizationDirectory(ILogger<GrpcOrganizationDirectory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _moduleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? "unknown";

        _coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
        if (string.IsNullOrWhiteSpace(_coreEndpoint))
        {
            _logger.LogWarning(
                "GrpcOrganizationDirectory: DOTNETCLOUD_CORE_ENDPOINT is not set, so the module cannot " +
                "reach Core.Server. Organization listings will be empty (module not launched by " +
                "ProcessSupervisor, or running in a test host).");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_coreEndpoint))
        {
            _logger.LogWarning(
                "GetUserOrganizations for {UserId} short-circuited: DOTNETCLOUD_CORE_ENDPOINT is not set, " +
                "so the module cannot reach Core.Server. No organizations will be reported for this user.",
                userId);
            return [];
        }

        try
        {
            var request = new GetUserOrganizationsRequest { UserId = userId.ToString() };

            var response = await GetClient().GetUserOrganizationsAsync(
                request, GetMetadata(), cancellationToken: cancellationToken);

            _logger.LogDebug("GetUserOrganizations gRPC response for {UserId}: {Count} organizations",
                userId, response.Organizations.Count);

            return response.Organizations.Select(MapOrganization).ToList();
        }
        catch (RpcException ex) when (IsConsentRequired(ex))
        {
            // A missing consent is not "no organizations": the caller has to ask the user, so an
            // empty list here would hide the prompt forever. Surface it (Email/Music precedent).
            throw;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            _logger.LogError(
                "GetUserOrganizations RPC is Unimplemented on Core.Server — the deployed core binary " +
                "predates the organization capability. Rebuild and redeploy Core.Server from the same " +
                "branch as this module. UserId={UserId}",
                userId);
            return [];
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning(
                "Core.Server gRPC service unavailable for GetUserOrganizations (StatusCode.Unavailable) — " +
                "core not reachable at DOTNETCLOUD_CORE_ENDPOINT='{Endpoint}'. UserId={UserId}",
                Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT"), userId);
            return [];
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning("GetUserOrganizations gRPC call to Core.Server timed out for {UserId}", userId);
            return [];
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "GetUserOrganizations gRPC call to Core.Server failed with status {StatusCode} for " +
                "{UserId}: {Detail}",
                ex.StatusCode, userId, ex.Status.Detail);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling GetUserOrganizations for {UserId}", userId);
            return [];
        }
    }

    /// <summary>
    /// Whether the core refused the call because the acting user has not consented to a scope.
    /// </summary>
    /// <remarks>
    /// The core answers <c>PermissionDenied</c> with the <c>error-code: MODULE_CONSENT_REQUIRED</c>
    /// trailer and lists the missing scopes in <c>consent-scopes</c>. That failure must reach the
    /// caller so it can prompt the user — degrading it to an empty list, the way this client handles
    /// an unreachable core, would leave the module permanently without organizations and with no way
    /// to ask for consent. Found live (Tracks, 2026-11-11).
    /// </remarks>
    /// <param name="exception">The failed core call.</param>
    /// <returns><see langword="true"/> when the call needs the user's consent.</returns>
    public static bool IsConsentRequired(RpcException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.StatusCode == StatusCode.PermissionDenied
            && string.Equals(
                exception.Trailers?.GetValue("error-code"),
                "MODULE_CONSENT_REQUIRED",
                StringComparison.Ordinal);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Answered from the user's organization listing. This is fail-closed: if the core is
    /// unreachable, the caller is reported as a non-member rather than being allowed through.
    /// </remarks>
    public async Task<bool> IsOrganizationMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var organizations = await GetUserOrganizationsAsync(userId, cancellationToken);
        return organizations.Any(o => o.Id == organizationId);
    }

    /// <inheritdoc />
    public Task<OrganizationMemberInfo?> GetMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetMemberAsync is not supported via gRPC yet");
        return Task.FromResult<OrganizationMemberInfo?>(null);
    }

    /// <inheritdoc />
    public Task<bool> HasOrgRoleAsync(
        Guid organizationId,
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "HasOrgRoleAsync is not exposed over gRPC; returning false (fail-closed). Module {ModuleId} " +
            "must not rely on org-role checks until the capability is extended.",
            _moduleId);
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> HasManagerOrAboveRoleAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "HasManagerOrAboveRoleAsync is not exposed over gRPC; returning false (fail-closed). Module " +
            "{ModuleId} must not rely on org-role checks until the capability is extended.",
            _moduleId);
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> GetUserRoleIdsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetUserRoleIdsAsync is not supported via gRPC yet");
        return Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
    }

    private CoreCapabilities.CoreCapabilitiesClient GetClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_client is not null)
            return _client;

        lock (_sync)
        {
            if (_client is not null)
                return _client;

            // Convert from the internal URL format used by ProcessSupervisor.
            var address = _coreEndpoint!
                .Replace("unix://", "http://")
                .Replace("net.pipe://", "http://");

            _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AllowAutoRedirect = false,
                    UseCookies = false,
                },
                ThrowOperationCanceledOnCancellation = true,
            });

            _client = new CoreCapabilities.CoreCapabilitiesClient(_channel);

            _logger.LogInformation(
                "GrpcOrganizationDirectory: connected to Core.Server at {Endpoint} (module: {ModuleId})",
                address, _moduleId);

            return _client;
        }
    }

    private Metadata GetMetadata()
    {
        if (string.IsNullOrWhiteSpace(_moduleId))
        {
            return new Metadata();
        }

        return ModuleIdentityMetadata.Create(_moduleId);
    }

    private static OrganizationDto MapOrganization(OrganizationInfoMessage message)
    {
        Guid.TryParse(message.Id, out var id);
        DateTime.TryParse(message.CreatedAt, out var createdAt);

        return new OrganizationDto
        {
            Id = id,
            Name = message.Name,
            Description = string.IsNullOrEmpty(message.Description) ? null : message.Description,
            MemberCount = message.MemberCount,
            TeamCount = message.TeamCount,
            CreatedAt = createdAt == default ? DateTime.UtcNow : createdAt,
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _channel?.Dispose();
    }
}
