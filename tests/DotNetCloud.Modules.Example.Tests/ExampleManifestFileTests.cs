using System.Text.Json;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Guards the packaged <c>manifest.json</c>. The store rejects a package that is not v2 or that has no
/// publisher, and the core rejects <c>dataStore: "isolated"</c> without <c>handlesUserErasure</c>, so
/// these invariants are asserted against the file that actually ships inside the <c>.dncpkg</c>.
/// </summary>
[TestClass]
public class ExampleManifestFileTests
{
    private JsonElement _manifest;

    [TestInitialize]
    public void Setup()
    {
        _manifest = LoadManifest();
    }

    [TestMethod]
    public void Manifest_IsVersion2()
    {
        Assert.AreEqual(2, _manifest.GetProperty("manifestVersion").GetInt32());
    }

    [TestMethod]
    public void Manifest_DeclaresHttpsPublisher()
    {
        var publisher = _manifest.GetProperty("publisher");

        Assert.AreEqual("dotnetcloud", publisher.GetProperty("id").GetString());
        Assert.AreEqual("DotNetCloud", publisher.GetProperty("name").GetString());

        var url = publisher.GetProperty("url").GetString();
        Assert.IsNotNull(url);
        Assert.IsTrue(url.StartsWith("https://", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Manifest_DeclaresIsolatedDataStoreAndErasure()
    {
        Assert.AreEqual("isolated", _manifest.GetProperty("dataStore").GetString());
        Assert.IsTrue(_manifest.GetProperty("handlesUserErasure").GetBoolean());
    }

    [TestMethod]
    public void Manifest_IdentityMatchesCodeManifest()
    {
        var code = new ExampleModuleManifest();

        Assert.AreEqual(code.Id, _manifest.GetProperty("id").GetString());
        Assert.AreEqual(code.Name, _manifest.GetProperty("name").GetString());
        Assert.AreEqual(code.Version, _manifest.GetProperty("version").GetString());
    }

    [TestMethod]
    public void Manifest_DeclaresCapabilitiesAndEventsInV2Fields()
    {
        var capabilities = _manifest.GetProperty("capabilities")
            .EnumerateArray()
            .Select(entry => entry.GetString())
            .ToList();

        CollectionAssert.Contains(capabilities, "INotificationService");
        CollectionAssert.Contains(capabilities, "IStorageProvider");
        CollectionAssert.Contains(capabilities, "IAuditLogger");

        var publishes = _manifest.GetProperty("events").GetProperty("publishes")
            .EnumerateArray()
            .Select(entry => entry.GetString())
            .ToList();

        CollectionAssert.Contains(publishes, "NoteCreatedEvent");
        CollectionAssert.Contains(publishes, "NoteDeletedEvent");
    }

    [TestMethod]
    public void Manifest_DoesNotUseLegacyV1Fields()
    {
        Assert.IsFalse(_manifest.TryGetProperty("author", out _), "v2 uses the publisher block.");
        Assert.IsFalse(_manifest.TryGetProperty("requiredCapabilities", out _), "v2 uses capabilities.");
        Assert.IsFalse(_manifest.TryGetProperty("publishedEvents", out _), "v2 uses the events block.");
        Assert.IsFalse(_manifest.TryGetProperty("subscribedEvents", out _), "v2 uses the events block.");
    }

    private static JsonElement LoadManifest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "manifest.json");
        Assert.IsTrue(
            File.Exists(path),
            $"manifest.json was not copied to the test output ({path}); check the test project's None item.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }
}
