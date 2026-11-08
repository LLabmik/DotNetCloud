using DotNetCloud.Core.Server.Sandbox;

namespace DotNetCloud.Core.Server.Tests.Sandbox;

/// <summary>
/// Tests for <see cref="ModuleLegacySchema"/>: the schema a bundled module used before it became
/// sandboxed is derived from its id, and only for the bundled namespace.
/// </summary>
/// <remarks>
/// The resolver feeds a schema name into DDL that moves objects, so it is also a security boundary:
/// a store package must not be able to talk the core into moving objects out of <c>core</c> or any
/// other schema by choosing an id.
/// </remarks>
[TestClass]
public class ModuleLegacySchemaTests
{
    [TestMethod]
    [DataRow("dotnetcloud.bookmarks", "bookmarks")]
    [DataRow("dotnetcloud.photos", "photos")]
    [DataRow("dotnetcloud.tracks", "tracks")]
    public void TryResolve_BundledOptionalModule_ReturnsHistoricalSchema(string moduleId, string expected)
    {
        var resolved = ModuleLegacySchema.TryResolve(moduleId, out var schema, out var reason);

        Assert.IsTrue(resolved, reason);
        Assert.AreEqual(expected, schema);
    }

    [TestMethod]
    [DataRow("dotnetcloud.files")]
    [DataRow("dotnetcloud.notes")]
    public void TryResolve_RequiredModule_IsRefused(string moduleId)
    {
        var resolved = ModuleLegacySchema.TryResolve(moduleId, out var schema, out var reason);

        Assert.IsFalse(resolved);
        Assert.AreEqual(string.Empty, schema);
        StringAssert.Contains(reason, "Required");
    }

    [TestMethod]
    [DataRow("org.example.customapp")]
    [DataRow("com.thirdparty.store")]
    public void TryResolve_ThirdPartyModule_IsRefused(string moduleId)
    {
        var resolved = ModuleLegacySchema.TryResolve(moduleId, out _, out var reason);

        Assert.IsFalse(resolved);
        StringAssert.Contains(reason, "bundled");
    }

    [TestMethod]
    [DataRow("dotnetcloud.")]
    [DataRow("dotnetcloud.bookmarks; DROP TABLE core.users")]
    [DataRow("dotnetcloud.book marks")]
    [DataRow("dotnetcloud..")]
    [DataRow("dotnetcloud.1bookmarks")]
    public void TryResolve_HostileOrEmptyName_IsRefused(string moduleId)
    {
        var resolved = ModuleLegacySchema.TryResolve(moduleId, out _, out var reason);

        Assert.IsFalse(resolved);
        Assert.IsNotNull(reason);
    }

    [TestMethod]
    public void TryResolve_MixedCaseBundledId_ResolvesToLowerCaseSchema()
    {
        var resolved = ModuleLegacySchema.TryResolve("DotNetCloud.Bookmarks", out var schema, out var reason);

        Assert.IsTrue(resolved, reason);
        Assert.AreEqual("bookmarks", schema);
    }
}
