using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.Core.Server.Tests.Consent;

/// <summary>
/// Tests for <see cref="ModuleConsentPolicy"/>: the hard check is narrowed to what a module
/// declared, the soft check is not, and only the hard check denies a call.
/// </summary>
[TestClass]
public class ModuleConsentPolicyTests
{
    private const string ModuleId = "vendor.receipts";

    private static readonly Guid UserId = Guid.CreateVersion7();

    private static ModuleConsentPolicy CreatePolicy(
        IReadOnlyList<string>? declaredScopes,
        IModuleConsentService? consent = null)
    {
        var manifests = new ModuleManifestRegistry();

        manifests.Record(ModuleId, new ModuleManifestData
        {
            Id = ModuleId,
            Name = "Receipts",
            Version = "1.0.0",
            ManifestVersion = 2,
            UserConsentScopes = declaredScopes ?? [],
        });

        return new ModuleConsentPolicy(manifests, consent ?? new ConsentTestDoubles.NoopConsentService());
    }

    [TestMethod]
    public async Task GetMissingConsentScopesAsync_ModuleDeclaresNoScopes_IsNotEnforced()
    {
        var policy = CreatePolicy(declaredScopes: []);

        var missing = await policy.GetMissingConsentScopesAsync(ModuleId, UserId, [ModuleScopes.Profile]);

        Assert.AreEqual(
            0,
            missing.Count,
            "A module that never opted into the consent plane keeps working on the capability grant alone.");
    }

    [TestMethod]
    public async Task GetMissingConsentScopesAsync_UnknownModule_IsNotEnforced()
    {
        var policy = CreatePolicy(declaredScopes: [ModuleScopes.Profile]);

        var missing = await policy.GetMissingConsentScopesAsync("not.recorded", UserId, [ModuleScopes.Profile]);

        Assert.AreEqual(0, missing.Count);
    }

    [TestMethod]
    public async Task GetMissingConsentScopesAsync_DeclaredButNotConsented_IsReported()
    {
        var policy = CreatePolicy(declaredScopes: [ModuleScopes.Profile]);

        var missing = await policy.GetMissingConsentScopesAsync(ModuleId, UserId, [ModuleScopes.Profile]);

        CollectionAssert.AreEqual(new[] { ModuleScopes.Profile }, missing.ToArray());
    }

    [TestMethod]
    public async Task GetMissingConsentScopesAsync_DeclaredAndConsented_IsEmpty()
    {
        var policy = CreatePolicy(
            declaredScopes: [ModuleScopes.Profile],
            new ConsentTestDoubles.GrantingConsentService([ModuleScopes.Profile]));

        var missing = await policy.GetMissingConsentScopesAsync(ModuleId, UserId, [ModuleScopes.Profile]);

        Assert.AreEqual(0, missing.Count);
    }

    [TestMethod]
    public async Task GetMissingConsentScopesAsync_ScopeNotDeclared_IsNeverRequired()
    {
        var policy = CreatePolicy(declaredScopes: [ModuleScopes.FilesRead]);

        var missing = await policy.GetMissingConsentScopesAsync(ModuleId, UserId, [ModuleScopes.Profile]);

        Assert.AreEqual(
            0,
            missing.Count,
            "A module cannot be denied for a scope its own manifest never claimed.");
    }

    [TestMethod]
    public async Task HasConsentAsync_IsNotNarrowedByTheManifest()
    {
        // The soft check protects personal data inside a wider result (an email address in a search
        // hit). A module that never declared `profile` must still not receive one.
        var policy = CreatePolicy(declaredScopes: [ModuleScopes.FilesRead]);

        Assert.IsFalse(await policy.HasConsentAsync(ModuleId, UserId, ModuleScopes.Profile));
    }

    [TestMethod]
    public void CreateConsentRequiredException_CarriesTheCodeAndTheScopes()
    {
        var exception = ModuleConsentPolicy.CreateConsentRequiredException([ModuleScopes.FilesRead, "profile"]);

        Assert.AreEqual(global::Grpc.Core.StatusCode.PermissionDenied, exception.StatusCode);
        Assert.AreEqual("MODULE_CONSENT_REQUIRED", exception.Trailers.GetValue("error-code"));
        Assert.AreEqual(
            "files:read profile",
            exception.Trailers.GetValue(ModuleConsentPolicy.MissingScopesTrailer));
    }
}
