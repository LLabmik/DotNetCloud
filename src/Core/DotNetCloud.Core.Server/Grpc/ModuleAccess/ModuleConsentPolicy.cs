using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Grpc.Core;

namespace DotNetCloud.Core.Server.Grpc.ModuleAccess;

/// <summary>
/// Applies the per-user consent plane to module calls.
/// </summary>
/// <remarks>
/// <para>
/// Two different questions are answered here, and conflating them is a security bug:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="GetMissingConsentScopesAsync"/> is the <b>hard</b> requirement. It is used where the
/// whole call exists to read one user's data (their team or group memberships). It only considers
/// scopes the module actually declared in its manifest, which keeps required and bundled modules —
/// which declare none today — working exactly as before.
/// </item>
/// <item>
/// <see cref="HasConsentAsync"/> is the <b>soft</b>, minimizing check used for personal data that
/// the call can do without (a search result's email address). It is not narrowed by the manifest:
/// a module that never declared <c>profile</c> still gets no email, because the declaration would
/// have been rejected at install time if the module wanted personal data.
/// </item>
/// </list>
/// </remarks>
internal sealed class ModuleConsentPolicy
{
    /// <summary>Trailer carrying the scopes the module must ask the user to approve.</summary>
    internal const string MissingScopesTrailer = "consent-scopes";

    private readonly IModuleManifestRegistry _manifests;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IModuleConsentService? _fixedConsent;

    /// <summary>Initializes a new instance of the <see cref="ModuleConsentPolicy"/> class.</summary>
    /// <param name="manifests">Declared manifest data per module.</param>
    /// <param name="scopeFactory">
    /// Scope factory used to resolve the (scoped) consent store. The policy itself is a singleton
    /// because every gRPC service it is injected into is a singleton.
    /// </param>
    public ModuleConsentPolicy(IModuleManifestRegistry manifests, IServiceScopeFactory scopeFactory)
    {
        _manifests = manifests;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Initializes a policy over a fixed consent store. Intended for tests.
    /// </summary>
    /// <param name="manifests">Declared manifest data per module.</param>
    /// <param name="consent">The consent store.</param>
    internal ModuleConsentPolicy(IModuleManifestRegistry manifests, IModuleConsentService consent)
    {
        _manifests = manifests;
        _fixedConsent = consent;
    }

    /// <summary>Gets the scopes a user has consented to for a module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The granted scopes.</returns>
    public Task<IReadOnlyList<string>> GetGrantedScopesAsync(
        string moduleId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        WithConsentAsync(
            (consent, token) => consent.GetGrantedScopesAsync(moduleId, userId, token),
            cancellationToken);

    /// <summary>
    /// Gets the declared scopes a module must obtain consent for, narrowed to the ones this call needs.
    /// </summary>
    /// <param name="moduleId">The authenticated module id.</param>
    /// <param name="userId">The user whose data the call touches.</param>
    /// <param name="requiredScopes">The scopes the call would need.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scopes still missing consent; empty when the call may proceed.</returns>
    public async Task<IReadOnlyList<string>> GetMissingConsentScopesAsync(
        string moduleId,
        Guid userId,
        IReadOnlyList<string> requiredScopes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleId) || userId == Guid.Empty || requiredScopes.Count == 0)
        {
            return [];
        }

        var declared = _manifests.GetUserConsentScopes(moduleId);

        // An unknown module or one that declares no scopes is not consent-aware yet. The capability
        // grant still gates the call; this transition is what keeps required/bundled modules running
        // until Track C migrates them.
        if (declared is null || declared.Count == 0)
        {
            return [];
        }

        var applicable = requiredScopes
            .Where(scope => declared.Contains(scope, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return applicable.Length == 0
            ? []
            : await WithConsentAsync(
                (consent, token) => consent.GetMissingScopesAsync(moduleId, userId, applicable, token),
                cancellationToken);
    }

    /// <summary>
    /// Gets the consent scopes a module declared in its manifest.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The declared scopes, or <see langword="null"/> when the module is unknown.</returns>
    public IReadOnlyCollection<string>? GetDeclaredScopes(string moduleId) =>
        _manifests.GetUserConsentScopes(moduleId);

    /// <summary>
    /// Determines whether a user consented to one scope for a module.
    /// </summary>
    /// <param name="moduleId">The authenticated module id.</param>
    /// <param name="userId">The user whose data would be returned.</param>
    /// <param name="scope">The scope the data falls under.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when an active consent row exists.</returns>
    public Task<bool> HasConsentAsync(
        string moduleId,
        Guid userId,
        string scope,
        CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(moduleId) || userId == Guid.Empty
            ? Task.FromResult(false)
            : WithConsentAsync(
                (consent, token) => consent.HasConsentAsync(moduleId, userId, scope, token),
                cancellationToken);

    /// <summary>
    /// Resolves the consent store for one call and runs <paramref name="action"/> against it.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="action">The work to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The action's result.</returns>
    private async Task<T> WithConsentAsync<T>(
        Func<IModuleConsentService, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (_fixedConsent is not null)
        {
            return await action(_fixedConsent, cancellationToken);
        }

        using var scope = _scopeFactory!.CreateScope();
        var consent = scope.ServiceProvider.GetRequiredService<IModuleConsentService>();
        return await action(consent, cancellationToken);
    }

    /// <summary>
    /// Builds the <c>MODULE_CONSENT_REQUIRED</c> failure for a call that cannot proceed.
    /// </summary>
    /// <param name="missingScopes">The scopes the user has not approved.</param>
    /// <returns>An exception carrying the error code and the scopes to request.</returns>
    public static RpcException CreateConsentRequiredException(IReadOnlyCollection<string> missingScopes)
    {
        ArgumentNullException.ThrowIfNull(missingScopes);

        var scopes = string.Join(' ', missingScopes);
        var metadata = new Metadata
        {
            { "error-code", ErrorCodes.ModuleConsentRequired },
            { MissingScopesTrailer, scopes },
        };

        return new RpcException(
            new Status(
                StatusCode.PermissionDenied,
                $"[{ErrorCodes.ModuleConsentRequired}] The user has not consented to the scopes this " +
                $"call needs: {scopes}. Ask the user for consent, then retry."),
            metadata);
    }
}
