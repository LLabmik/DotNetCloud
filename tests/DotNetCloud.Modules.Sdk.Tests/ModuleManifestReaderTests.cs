using DotNetCloud.Modules.Sdk.Manifest;

namespace DotNetCloud.Modules.Sdk.Tests;

/// <summary>
/// Tests for the SDK manifest reader: it must accept what the core accepts and reject the same
/// shapes, so a module author catches a bad manifest before packaging.
/// </summary>
[TestClass]
public class ModuleManifestReaderTests
{
    private const string ValidManifest = """
        {
          "manifestVersion": 2,
          "id": "org.example.customapp",
          "name": "Custom App",
          "version": "1.2.0",
          "description": "An example",
          "publisher": { "id": "example-org", "name": "Example Org", "url": "https://example.org" },
          "moduleApiVersion": 1,
          "minCoreVersion": "0.7.0",
          "dataStore": "isolated",
          "handlesUserErasure": true,
          "capabilities": ["INotificationService"],
          "userConsentScopes": ["files:read", "notifications:send"],
          "events": { "publishes": ["ThingCreatedEvent"], "subscribes": [] },
          "ui": {
            "type": "iframe",
            "entryPath": "/",
            "navItems": [{ "id": "custom", "label": "Custom", "icon": "widgets", "path": "/" }]
          },
          "resources": { "memoryLimitMb": 256, "cpuPercent": 25 },
          "restartPolicy": "backoff"
        }
        """;

    [TestMethod]
    public void Read_UnknownConsentScope_IsRejected()
    {
        var json = ValidManifest.Replace(
            "\"userConsentScopes\": [\"files:read\", \"notifications:send\"]",
            "\"userConsentScopes\": [\"files:read\", \"made-up:read\", \"notifications:send\"]");

        var result = ModuleManifestReader.Read(json);

        Assert.IsFalse(result.IsValid, "A scope the core cannot describe is a scope a user cannot approve.");
        StringAssert.Contains(string.Join("; ", result.Errors), "made-up:read");
    }

    [TestMethod]
    public void Read_UserDataCapabilityWithoutItsScope_IsRejected()
    {
        var json = ValidManifest.Replace(
            "\"userConsentScopes\": [\"files:read\", \"notifications:send\"]",
            "\"userConsentScopes\": [\"files:read\"]");

        var result = ModuleManifestReader.Read(json);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join("; ", result.Errors), "notifications:send");
    }

    [TestMethod]
    public void Read_ValidManifest_ParsesEverySection()
    {
        var result = ModuleManifestReader.Read(ValidManifest);

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        var manifest = result.Manifest!;
        Assert.AreEqual("org.example.customapp", manifest.Id);
        Assert.AreEqual(ModuleDataStoreModes.Isolated, manifest.DataStore);
        Assert.AreEqual("example-org", manifest.Publisher!.Id);
        Assert.AreEqual("ThingCreatedEvent", manifest.Events!.Publishes.Single());
        Assert.AreEqual("widgets", manifest.Ui!.NavItems.Single().Icon);
        Assert.AreEqual(256, manifest.Resources!.MemoryLimitMb);
    }

    [TestMethod]
    public void Read_UnknownTopLevelMember_IsRejected()
    {
        var json = ValidManifest.Replace(
            "\"restartPolicy\": \"backoff\"",
            "\"restartPolicy\": \"backoff\", \"mystery\": true");

        var result = ModuleManifestReader.Read(json);

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void Read_WidgetBlock_ParsesItsPath()
    {
        var json = ValidManifest.Replace(
            "\"navItems\": [{ \"id\": \"custom\", \"label\": \"Custom\", \"icon\": \"widgets\", \"path\": \"/\" }]",
            "\"navItems\": [{ \"id\": \"custom\", \"label\": \"Custom\", \"icon\": \"widgets\", \"path\": \"/\" }], \"widget\": { \"path\": \"/widget/index.html\" }");

        var result = ModuleManifestReader.Read(json);

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.AreEqual("/widget/index.html", result.Manifest!.Ui!.Widget!.Path);
    }

    [TestMethod]
    public void Read_WidgetPathWithoutLeadingSlash_IsRejected()
    {
        var json = ValidManifest.Replace(
            "\"navItems\": [{ \"id\": \"custom\", \"label\": \"Custom\", \"icon\": \"widgets\", \"path\": \"/\" }]",
            "\"navItems\": [{ \"id\": \"custom\", \"label\": \"Custom\", \"icon\": \"widgets\", \"path\": \"/\" }], \"widget\": { \"path\": \"widget/index.html\" }");

        var result = ModuleManifestReader.Read(json);

        Assert.IsFalse(result.IsValid, "a widget path must be site-relative, like ui.entryPath");
        StringAssert.Contains(string.Join("; ", result.Errors), "ui.widget.path");
    }

    [TestMethod]
    public void Read_TrailingComma_IsRejected()
    {
        var json = ValidManifest.Replace("\"cpuPercent\": 25 }", "\"cpuPercent\": 25, }");

        var result = ModuleManifestReader.Read(json);

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void Read_InvalidId_IsRejected()
    {
        var json = ValidManifest.Replace("org.example.customapp", "Org.Example.CustomApp");

        var result = ModuleManifestReader.Read(json);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join("; ", result.Errors), "reverse-DNS");
    }

    [TestMethod]
    public void Read_UnknownDataStoreMode_IsRejected()
    {
        var json = ValidManifest.Replace("\"dataStore\": \"isolated\"", "\"dataStore\": \"shared\"");

        var result = ModuleManifestReader.Read(json);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join("; ", result.Errors), "dataStore");
    }

    [TestMethod]
    public void Read_UserScopedModuleWithoutErasureSupport_IsRejected()
    {
        var json = ValidManifest.Replace("\"handlesUserErasure\": true", "\"handlesUserErasure\": false");

        var result = ModuleManifestReader.Read(json);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join("; ", result.Errors), "handlesUserErasure");
    }

    [TestMethod]
    public void Read_NonIframeUi_IsRejected()
    {
        var json = ValidManifest.Replace("\"type\": \"iframe\"", "\"type\": \"blazor\"");

        var result = ModuleManifestReader.Read(json);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join("; ", result.Errors), "iframe");
    }

    [TestMethod]
    public void Read_InvalidJson_IsRejectedWithAnError()
    {
        var result = ModuleManifestReader.Read("{ not json");

        Assert.IsFalse(result.IsValid);
        Assert.IsNull(result.Manifest);
        Assert.IsNotEmpty(result.Errors);
    }

    [TestMethod]
    public void Read_MissingFile_IsRejected()
    {
        var result = ModuleManifestReader.ReadFile("/nonexistent/manifest.json");

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join("; ", result.Errors), "does not exist");
    }
}
