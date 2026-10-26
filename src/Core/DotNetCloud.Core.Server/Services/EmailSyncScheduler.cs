using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Server.Grpc.Clients;
using DotNetCloud.Core.Server.Grpc.Interceptors;
using DotNetCloud.Core.Server.Supervisor;
using DotNetCloud.Core.Services;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EmailProto = DotNetCloud.Modules.Email.Host.Protos;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Drives email account syncs by invoking the Email module for a user the core vouches for.
/// </summary>
/// <remarks>
/// <para>
/// The Email module used to sweep every enabled account on its own timer, which meant the module
/// decided by itself that it was acting as each account's owner — an identity nobody verified. Now
/// the core schedules the work: it asks the module which accounts are due, confirms each owner with
/// the identity store, mints a short-lived ECDSA assertion naming that user for the email module,
/// and invokes the sync over the authenticated core→module channel. The module refuses a sync that
/// arrives without an assertion, or whose payload names a different user.
/// </para>
/// <para>
/// IMAP/SMTP/Gmail logic stays in the module. The core only decides <i>who</i> a sync runs as and
/// <i>when</i> it starts.
/// </para>
/// <para>
/// The module owns the shared channel (<see cref="ModuleEndpointProvider.GetChannel"/>): it must
/// never be disposed here, or every later core→email call fails.
/// </para>
/// </remarks>
internal sealed class EmailSyncScheduler : BackgroundService
{
    /// <summary>The module whose accounts are synced.</summary>
    internal const string ModuleId = "dotnetcloud.email";

    /// <summary>How often the scheduler looks for accounts that are due.</summary>
    internal static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Delay before the first scan. Module hosts register with the supervisor a little after the
    /// core starts; scanning immediately would only log a connection failure.
    /// </summary>
    internal static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    /// <summary>Per-call deadline for a sync — a large mailbox can take a while.</summary>
    internal static readonly TimeSpan SyncCallTimeout = TimeSpan.FromMinutes(2);

    private const string ServiceName = "Email Account Sync";

    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ModuleAssertionKeyRing _assertionKeyRing;
    private readonly ICallerIdentityResolver _identityResolver;
    private readonly IBackgroundServiceTracker? _tracker;
    private readonly ILogger<EmailSyncScheduler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSyncScheduler"/> class.
    /// </summary>
    public EmailSyncScheduler(
        ModuleEndpointProvider endpointProvider,
        ModuleAssertionKeyRing assertionKeyRing,
        ICallerIdentityResolver identityResolver,
        ILogger<EmailSyncScheduler> logger,
        IBackgroundServiceTracker? tracker = null)
    {
        _endpointProvider = endpointProvider;
        _assertionKeyRing = assertionKeyRing;
        _identityResolver = identityResolver;
        _logger = logger;
        _tracker = tracker;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "EmailSyncScheduler started. Scan interval: {Interval}, startup delay: {Delay}",
            ScanInterval, StartupDelay);

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(ScanInterval);

        do
        {
            try
            {
                await _tracker.RunTrackedAsync(
                    ServiceName, () => RunCycleAsync(stoppingToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailSyncScheduler cycle failed");
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));

        _logger.LogInformation("EmailSyncScheduler stopped");
    }

    /// <summary>
    /// One scheduling cycle: list the accounts the module says are due, then invoke a sync for each
    /// with an assertion naming its owner.
    /// </summary>
    internal async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var client = new EmailProto.EmailService.EmailServiceClient(
            _endpointProvider.GetChannel(ModuleId));

        EmailProto.ListSyncCandidatesResponse candidates;
        try
        {
            candidates = await client.ListSyncCandidatesAsync(
                new EmailProto.ListSyncCandidatesRequest
                {
                    MinAgeSeconds = (int)ScanInterval.TotalSeconds
                },
                deadline: DateTime.UtcNow.AddSeconds(30),
                cancellationToken: cancellationToken);
        }
        catch (RpcException ex)
        {
            // A module that is not running yet (or is restarting) is normal, not an error.
            _logger.LogDebug(
                "EmailSyncScheduler could not list sync candidates ({Status}): {Detail}",
                ex.StatusCode, ex.Status.Detail);
            return;
        }

        if (!candidates.Success)
        {
            _logger.LogWarning(
                "Email module refused to list sync candidates: {Error}", candidates.ErrorMessage);
            return;
        }

        if (candidates.Candidates.Count == 0)
            return;

        var synced = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var candidate in candidates.Candidates)
        {
            if (!TryParseCandidate(candidate, out var accountId, out var userId))
            {
                _logger.LogWarning(
                    "EmailSyncScheduler skipped a candidate with unparsable ids ({AccountId}/{UserId})",
                    candidate.AccountId, candidate.UserId);
                skipped++;
                continue;
            }

            // Only vouch for accounts that exist and are active. Minting an assertion for an
            // unknown or disabled user would let stale module data keep acting on their behalf.
            if (!await CanActForUserAsync(userId, cancellationToken))
            {
                _logger.LogInformation(
                    "EmailSyncScheduler skipped account {AccountId}: user {UserId} is unknown or inactive",
                    accountId, userId);
                skipped++;
                continue;
            }

            try
            {
                var assertion = _assertionKeyRing.Mint(userId, ModuleId, operation: "email.sync");

                var response = await client.TriggerSyncAsync(
                    new EmailProto.TriggerSyncRequest
                    {
                        AccountId = accountId.ToString(),
                        UserId = userId.ToString()
                    },
                    new CallOptions(
                        headers: new Metadata { { ModuleIdentityMetadata.UserAssertionHeader, assertion } },
                        deadline: DateTime.UtcNow.Add(SyncCallTimeout),
                        cancellationToken: cancellationToken));

                if (response.Success)
                {
                    synced++;
                }
                else
                {
                    failed++;
                    _logger.LogWarning(
                        "Email sync for account {AccountId} was not accepted: {Error}",
                        accountId, response.ErrorMessage);
                }
            }
            catch (RpcException ex)
            {
                failed++;
                _logger.LogWarning(
                    "Email sync for account {AccountId} failed ({Status}): {Detail}",
                    accountId, ex.StatusCode, ex.Status.Detail);
            }
        }

        _logger.LogInformation(
            "EmailSyncScheduler cycle complete: {Synced} synced, {Skipped} skipped, {Failed} failed",
            synced, skipped, failed);
    }

    /// <summary>
    /// Decides whether the core may vouch for <paramref name="userId"/> on an email sync.
    /// </summary>
    /// <param name="userId">The user the module reported as an account owner.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>
    /// <see langword="true"/> only when the user exists and is active; an unknown or disabled
    /// account must not be able to have its mailboxes read on the strength of stale module data.
    /// </returns>
    internal async Task<bool> CanActForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            return false;

        var roles = await _identityResolver.ResolveRolesAsync(userId, cancellationToken);
        return roles is not null;
    }

    /// <summary>
    /// Parses the ids of a candidate reported by the module.
    /// </summary>
    /// <remarks>
    /// The ids come from another process, so they are validated before the core mints an assertion
    /// for them: an empty or unparsable user id must never reach the signer.
    /// </remarks>
    /// <param name="candidate">The candidate reported by the Email module.</param>
    /// <param name="accountId">The parsed account id.</param>
    /// <param name="userId">The parsed user id.</param>
    /// <returns><see langword="true"/> when both ids are usable.</returns>
    internal static bool TryParseCandidate(
        EmailProto.SyncCandidate candidate,
        out Guid accountId,
        out Guid userId)
    {
        accountId = Guid.Empty;
        userId = Guid.Empty;

        if (candidate is null)
            return false;

        return Guid.TryParse(candidate.AccountId, out accountId)
            && Guid.TryParse(candidate.UserId, out userId)
            && accountId != Guid.Empty
            && userId != Guid.Empty;
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
