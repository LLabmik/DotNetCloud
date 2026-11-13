using DotNetCloud.Core.Modules;

namespace DotNetCloud.Core.Server.Tests.Consent;

/// <summary>
/// Tests for the shared scope catalog: it is the single source every other component reads, so
/// duplicates, unknown names and undescribed scopes are correctness bugs, not cosmetics.
/// </summary>
[TestClass]
public class ModuleScopesTests
{
    [TestMethod]
    public void ConsentScopes_AreUnique()
    {
        var duplicates = ModuleScopes.ConsentScopes
            .GroupBy(scope => scope.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.AreEqual(0, duplicates.Length, $"Duplicate scopes: {string.Join(", ", duplicates)}");
    }

    [TestMethod]
    public void ConsentScopes_AllHaveAPlainLanguageDescription()
    {
        foreach (var scope in ModuleScopes.ConsentScopes)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(scope.Description),
                $"Scope '{scope.Name}' has no description; a user cannot consent to something undescribed.");
        }
    }

    [TestMethod]
    public void RegisteredScopes_ContainEveryConsentScopePlusStandardOidcScopes()
    {
        foreach (var scope in ModuleScopes.ConsentScopes)
        {
            CollectionAssert.Contains(
                ModuleScopes.RegisteredScopes.ToList(),
                scope.Name,
                $"'{scope.Name}' must be registered with OpenIddict.");
        }

        foreach (var standard in new[] { "openid", "email", "offline_access" })
        {
            CollectionAssert.Contains(ModuleScopes.RegisteredScopes.ToList(), standard);
        }
    }

    [TestMethod]
    public void RegisteredScopes_AreUnique()
    {
        var duplicates = ModuleScopes.RegisteredScopes
            .GroupBy(scope => scope, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.AreEqual(0, duplicates.Length, $"Duplicate registrations: {string.Join(", ", duplicates)}");
    }

    [TestMethod]
    public void IsConsentScope_ProfileIsAConsentScope()
    {
        Assert.IsTrue(ModuleScopes.IsConsentScope(ModuleScopes.Profile));
        Assert.IsTrue(ModuleScopes.IsKnown(ModuleScopes.Profile));
    }

    [TestMethod]
    public void IsConsentScope_StandardOidcScope_IsNotAConsentRecord()
    {
        Assert.IsFalse(ModuleScopes.IsConsentScope("offline_access"));
        Assert.IsTrue(ModuleScopes.IsKnown("offline_access"));
    }

    [TestMethod]
    public void FindUnknown_ReportsOnlyUnknownScopes()
    {
        var unknown = ModuleScopes.FindUnknown(["files:read", "made-up:read", "profile"]);

        CollectionAssert.AreEqual(new[] { "made-up:read" }, unknown.ToArray());
    }

    [TestMethod]
    public void Describe_UnknownScope_FallsBackToTheRawName()
    {
        var description = ModuleScopes.Describe("made-up:read");

        StringAssert.Contains(description, "made-up:read");
    }

    [TestMethod]
    public void CapabilityScopes_UserDataCapabilities_MapToAScope()
    {
        CollectionAssert.AreEqual(
            new[] { ModuleScopes.Profile },
            ModuleCapabilityScopes.RequiredFor("IUserDirectory").ToArray());

        CollectionAssert.AreEqual(
            new[] { ModuleScopes.NotificationsSend },
            ModuleCapabilityScopes.RequiredFor("INotificationService").ToArray());

        Assert.AreEqual(0, ModuleCapabilityScopes.RequiredFor("IEventBus").Count);
    }

    [TestMethod]
    public void CapabilityScopes_MappedScopes_AreInTheCatalog()
    {
        foreach (var (capability, scopes) in ModuleCapabilityScopes.CapabilityToScopes)
        {
            foreach (var scope in scopes)
            {
                Assert.IsTrue(
                    ModuleScopes.IsConsentScope(scope),
                    $"Capability '{capability}' maps to '{scope}', which is not a consent scope.");
            }
        }
    }

    [TestMethod]
    public void RequiredFor_MultipleCapabilities_ReturnsTheDistinctUnion()
    {
        var scopes = ModuleCapabilityScopes.RequiredFor(["IUserDirectory", "ICurrentUserContext", "IEventBus"]);

        CollectionAssert.AreEqual(new[] { ModuleScopes.Profile }, scopes.ToArray());
    }
}
