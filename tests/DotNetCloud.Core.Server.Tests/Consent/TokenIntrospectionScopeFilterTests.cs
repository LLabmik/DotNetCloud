using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.Services;

namespace DotNetCloud.Core.Server.Tests.Consent;

/// <summary>
/// Tests for the token-introspection scope narrowing: a module is never told it holds a stronger
/// grant than its manifest claims or the token's user approved.
/// </summary>
[TestClass]
public class TokenIntrospectionScopeFilterTests
{
    private static readonly IReadOnlyList<string> TokenScopes =
        ["openid", "profile", "files:read", "files:write", "offline_access"];

    [TestMethod]
    public void FilterScopes_ModuleDeclaresNothing_KeepsTheTokenScopes()
    {
        var scopes = TokenIntrospectionServiceImpl.FilterScopes(
            declared: [],
            consented: null,
            TokenScopes);

        CollectionAssert.AreEquivalent(TokenScopes.ToArray(), scopes.ToArray());
    }

    [TestMethod]
    public void FilterScopes_UnknownModule_KeepsTheTokenScopes()
    {
        var scopes = TokenIntrospectionServiceImpl.FilterScopes(
            declared: null,
            consented: null,
            TokenScopes);

        CollectionAssert.AreEquivalent(TokenScopes.ToArray(), scopes.ToArray());
    }

    [TestMethod]
    public void FilterScopes_DeclaredWithoutConsent_DropsTheConsentScope()
    {
        var scopes = TokenIntrospectionServiceImpl.FilterScopes(
            declared: [ModuleScopes.FilesRead, ModuleScopes.FilesWrite],
            consented: null,
            TokenScopes);

        CollectionAssert.DoesNotContain(scopes.ToArray(), ModuleScopes.FilesRead);
        CollectionAssert.DoesNotContain(scopes.ToArray(), ModuleScopes.FilesWrite);
        CollectionAssert.Contains(scopes.ToArray(), "openid");
        CollectionAssert.Contains(scopes.ToArray(), "offline_access");
    }

    [TestMethod]
    public void FilterScopes_DeclaredAndConsented_KeepsTheIntersection()
    {
        var scopes = TokenIntrospectionServiceImpl.FilterScopes(
            declared: [ModuleScopes.FilesRead],
            consented: new HashSet<string>([ModuleScopes.FilesRead, ModuleScopes.FilesWrite], StringComparer.Ordinal),
            TokenScopes);

        CollectionAssert.Contains(scopes.ToArray(), ModuleScopes.FilesRead);
        CollectionAssert.DoesNotContain(
            scopes.ToArray(),
            ModuleScopes.FilesWrite,
            "The module never declared files:write, so consent cannot widen it.");
    }

    [TestMethod]
    public void FilterScopes_UndeclaredNonConsentScope_IsDropped()
    {
        var scopes = TokenIntrospectionServiceImpl.FilterScopes(
            declared: [ModuleScopes.FilesRead],
            consented: new HashSet<string>([ModuleScopes.FilesRead], StringComparer.Ordinal),
            ["openid", "email", "offline_access"]);

        CollectionAssert.DoesNotContain(scopes.ToArray(), "email");
        CollectionAssert.Contains(scopes.ToArray(), "openid");
    }

    [TestMethod]
    public void FilterScopes_UnrecognisedScope_IsNotGatedOnConsent()
    {
        // A scope the catalog does not know was never a consent decision, so it is not filtered.
        var scopes = TokenIntrospectionServiceImpl.FilterScopes(
            declared: [ModuleScopes.FilesRead],
            consented: new HashSet<string>([ModuleScopes.FilesRead], StringComparer.Ordinal),
            ["custom:something"]);

        CollectionAssert.Contains(scopes.ToArray(), "custom:something");
    }

    [TestMethod]
    public void FilterScopes_Duplicates_AreCollapsed()
    {
        var scopes = TokenIntrospectionServiceImpl.FilterScopes(
            declared: ["openid"],
            consented: null,
            ["openid", "openid"]);

        Assert.AreEqual(1, scopes.Count);
    }
}
