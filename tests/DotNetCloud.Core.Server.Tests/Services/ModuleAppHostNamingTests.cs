using DotNetCloud.UI.Web.Components.Modules;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// The permission bar names the module in plain language.
/// </summary>
/// <remarks>
/// A module without a manifest is registered under its reverse-DNS id, and the bar read
/// "dotnetcloud.music is asking for permission" in the first live run. The first sentence a user
/// reads about a module must be its name, not its identifier.
/// </remarks>
[TestClass]
public class ModuleAppHostNamingTests
{
    /// <summary>A real display name passes through untouched.</summary>
    [TestMethod]
    public void ToDisplayName_WithManifestName_ReturnsIt()
    {
        Assert.AreEqual("Music", ModuleAppHost.ToDisplayName("Music", "dotnetcloud.music"));
    }

    /// <summary>The reverse-DNS fallback is humanised instead of shown raw.</summary>
    [TestMethod]
    public void ToDisplayName_WithModuleIdFallback_HumanisesTheLastSegment()
    {
        Assert.AreEqual("Music", ModuleAppHost.ToDisplayName("dotnetcloud.music", "dotnetcloud.music"));
        Assert.AreEqual("My App", ModuleAppHost.ToDisplayName("org.example.my-app", "org.example.my-app"));
        Assert.AreEqual("Custom App", ModuleAppHost.ToDisplayName("vendor.custom_app", "vendor.custom_app"));
    }

    /// <summary>An empty title falls back to the id rather than rendering " is asking for permission".</summary>
    [TestMethod]
    public void ToDisplayName_WithEmptyTitle_UsesTheModuleId()
    {
        Assert.AreEqual("Contacts", ModuleAppHost.ToDisplayName(null, "dotnetcloud.contacts"));
        Assert.AreEqual("Contacts", ModuleAppHost.ToDisplayName("  ", "dotnetcloud.contacts"));
    }
}
