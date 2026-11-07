using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.Core.Server.Tests.Consent;

/// <summary>
/// Consent doubles for tests that are not about the consent plane.
/// </summary>
/// <remarks>
/// A test that exercises capability handlers should not have to build a consent store; the
/// transitional module rule ("declares no scopes ⇒ no consent required") makes an empty manifest
/// registry and an empty consent store the correct no-op.
/// </remarks>
internal static class ConsentTestDoubles
{
    /// <summary>Creates a policy where no module has declared consent scopes.</summary>
    public static ModuleConsentPolicy NoConsent() => new(new ModuleManifestRegistry(), new NoopConsentService());

    /// <summary>Creates a policy over the supplied registry and consent store.</summary>
    public static ModuleConsentPolicy Policy(
        IModuleManifestRegistry manifests,
        IModuleConsentService consent) => new(manifests, consent);

    /// <summary>An in-memory consent store holding a mutable granted-scope set.</summary>
    public class FakeConsentService : IModuleConsentService
    {
        private readonly HashSet<string> _granted;

        public FakeConsentService(IEnumerable<string> grantedScopes)
        {
            _granted = new HashSet<string>(grantedScopes, StringComparer.Ordinal);
        }

        public IReadOnlyList<string> Granted => _granted.ToArray();

        /// <summary>How many times every consent row for a user was deleted (account erasure).</summary>
        public int DeleteAllForUserCalls { get; private set; }

        public Task<IReadOnlyList<string>> GetGrantedScopesAsync(string moduleId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Granted);

        public Task<IReadOnlyList<string>> GetMissingScopesAsync(string moduleId, Guid userId, IReadOnlyCollection<string> requiredScopes, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(
                requiredScopes.Where(scope => !_granted.Contains(scope)).ToArray());

        public Task<bool> HasConsentAsync(string moduleId, Guid userId, string scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_granted.Contains(scope));

        public Task<int> GrantAsync(string moduleId, Guid userId, IReadOnlyCollection<string> scopes, Guid? grantedByUserId = null, DateTime? expiresAtUtc = null, CancellationToken cancellationToken = default)
        {
            foreach (var scope in scopes)
            {
                _granted.Add(scope);
            }

            return Task.FromResult(scopes.Count);
        }

        public Task<int> RevokeAsync(string moduleId, Guid userId, string scope, Guid? revokedByUserId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_granted.Remove(scope) ? 1 : 0);

        public Task<int> RevokeAllAsync(string moduleId, Guid userId, Guid? revokedByUserId = null, CancellationToken cancellationToken = default)
        {
            var count = _granted.Count;
            _granted.Clear();
            return Task.FromResult(count);
        }

        public Task<int> RevokeAllForModuleAsync(string moduleId, Guid? revokedByUserId = null, CancellationToken cancellationToken = default)
        {
            var count = _granted.Count;
            _granted.Clear();
            return Task.FromResult(count);
        }

        public Task<int> DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            DeleteAllForUserCalls++;
            var count = _granted.Count;
            _granted.Clear();
            return Task.FromResult(count);
        }

        public Task<IReadOnlyList<ModuleConsentSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ModuleConsentSummary>>([]);

        public Task<IReadOnlyList<ModuleConsentUserSummary>> ListForModuleAsync(string moduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ModuleConsentUserSummary>>([]);

        public Task<ModuleConsentOverview> GetOverviewAsync(string moduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModuleConsentOverview(0, []));

        public void InvalidateCache(string moduleId, Guid userId)
        {
        }
    }

    /// <summary>A consent store that grants nothing and stores nothing.</summary>
    public sealed class NoopConsentService : FakeConsentService
    {
        public NoopConsentService()
            : base([])
        {
        }
    }

    /// <summary>A consent store that already holds the supplied scopes.</summary>
    public sealed class GrantingConsentService : FakeConsentService
    {
        public GrantingConsentService(IEnumerable<string> grantedScopes)
            : base(grantedScopes)
        {
        }
    }
}
