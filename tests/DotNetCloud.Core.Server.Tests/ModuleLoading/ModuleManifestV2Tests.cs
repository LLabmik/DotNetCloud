using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Tests for manifest v2 parsing and validation.
/// </summary>
[TestClass]
public class ModuleManifestV2Tests
{
    private readonly ModuleManifestLoader _loader = new(NullLogger<ModuleManifestLoader>.Instance);

    private string WriteManifest(string json)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dnc-manifest-tests", Guid.CreateVersion7().ToString());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "manifest.json");
        File.WriteAllText(path, json);
        return path;
    }

    private const string ValidV2 = """
        {
          "manifestVersion": 2,
          "id": "org.example.customapp",
          "name": "Custom App",
          "version": "1.2.0",
          "description": "Example",
          "publisher": { "id": "example-org", "name": "Example Org", "url": "https://example.org" },
          "moduleApiVersion": 1,
          "minCoreVersion": "0.7.0",
          "dataStore": "isolated",
          "handlesUserErasure": true,
          "capabilities": ["INotificationService", "IStorageProvider"],
          "userConsentScopes": ["files:read", "calendar:write", "notifications:send"],
          "events": { "publishes": ["ThingCreatedEvent"], "subscribes": ["FileUploadedEvent"] },
          "ui": {
            "type": "iframe",
            "entryPath": "/",
            "navItems": [ { "id": "custom", "label": "Custom App", "icon": "widgets", "path": "/" } ]
          },
          "resources": { "memoryLimitMb": 256, "cpuPercent": 25, "dataDirMb": 1024, "databaseMb": 2048, "connectionLimit": 10 },
          "restartPolicy": "backoff"
        }
        """;

    [TestMethod]
    public void LoadAndValidate_ValidV2Manifest_Succeeds()
    {
        var result = _loader.LoadAndValidate(WriteManifest(ValidV2), "org.example.customapp");

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Manifest);
        Assert.AreEqual(2, result.Manifest.ManifestVersion);
        Assert.AreEqual(ModuleDataStoreMode.Isolated, result.Manifest.EffectiveDataStoreMode);
        CollectionAssert.Contains(result.Manifest.EffectiveCapabilities.ToList(), "IStorageProvider");
        CollectionAssert.Contains(result.Manifest.EffectivePublishedEvents.ToList(), "ThingCreatedEvent");
        CollectionAssert.Contains(result.Manifest.EffectiveSubscribedEvents.ToList(), "FileUploadedEvent");
        Assert.AreEqual(256, result.Manifest.EffectiveMemoryLimitMb);
    }

    [TestMethod]
    public void LoadAndValidate_V2UnknownTopLevelField_Fails()
    {
        var json = ValidV2.Replace("\"restartPolicy\": \"backoff\"", "\"restartPolicy\": \"backoff\", \"surprise\": true");

        var result = _loader.LoadAndValidate(WriteManifest(json), "org.example.customapp");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("JSON")));
    }

    [TestMethod]
    public void LoadAndValidate_V2WithComments_Fails()
    {
        var json = ValidV2.Replace(
            "\"manifestVersion\"",
            "// comment\n  \"manifestVersion\"",
            StringComparison.Ordinal);

        var result = _loader.LoadAndValidate(WriteManifest(json), "org.example.customapp");

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void Validate_V2TrailingCommaInObject_Fails()
    {
        var json = ValidV2.Replace("\"connectionLimit\": 10 }", "\"connectionLimit\": 10, }");

        var result = _loader.LoadAndValidate(WriteManifest(json), "org.example.customapp");

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void Validate_InvalidDataStore_Fails()
    {
        var result = _loader.Validate(
            CreateManifest(dataStore: "shared"),
            "org.example.customapp");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("dataStore")));
    }

    [TestMethod]
    public void Validate_IsolatedStoreWithoutErasureSupport_Fails()
    {
        var result = _loader.Validate(
            CreateManifest(dataStore: "isolated", handlesUserErasure: null),
            "org.example.customapp");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("handlesUserErasure")));
    }

    [TestMethod]
    public void Validate_ConsentScopesWithoutErasureSupport_Fails()
    {
        var result = _loader.Validate(
            CreateManifest(dataStore: "none", handlesUserErasure: null, scopes: ["files:read"]),
            "org.example.customapp");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("handlesUserErasure")));
    }

    [TestMethod]
    public void Validate_NoneStoreWithoutUserData_DoesNotRequireErasure()
    {
        var result = _loader.Validate(
            CreateManifest(dataStore: "none", handlesUserErasure: null),
            "org.example.customapp");

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [TestMethod]
    [DataRow("Org.Example.App")]
    [DataRow("org.example_app")]
    [DataRow("org..example")]
    public void Validate_StrictModuleIdRules_RejectInvalidIds(string id)
    {
        var manifest = CreateManifest(dataStore: "none") with { Id = id };

        var result = _loader.Validate(manifest, id);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Module ID")));
    }

    [TestMethod]
    public void Validate_InvalidConsentScope_Fails()
    {
        var result = _loader.Validate(
            CreateManifest(dataStore: "none", handlesUserErasure: true, scopes: ["files:delete"]),
            "org.example.customapp");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("userConsentScope")));
    }

    [TestMethod]
    public void Validate_NonIframeUi_Fails()
    {
        var result = _loader.Validate(
            CreateManifest(dataStore: "none") with
            {
                Ui = new ManifestUi { Type = "blazor" }
            },
            "org.example.customapp");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("ui.type")));
    }

    [TestMethod]
    public void Validate_NonMaterialIconName_Fails()
    {
        var result = _loader.Validate(
            CreateManifest(dataStore: "none") with
            {
                Ui = new ManifestUi
                {
                    NavItems = [new ManifestNavItem { Id = "x", Label = "X", Icon = "🏠" }]
                }
            },
            "org.example.customapp");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Material icon")));
    }

    [TestMethod]
    public void Validate_NonPositiveResourceRequest_Fails()
    {
        var result = _loader.Validate(
            CreateManifest(dataStore: "none") with
            {
                Resources = new ManifestResources { MemoryLimitMb = 0 }
            },
            "org.example.customapp");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("resources.MemoryLimitMb")));
    }

    [TestMethod]
    public void Validate_RequiredModuleDeclaringDataStore_Fails()
    {
        var manifest = CreateManifest(dataStore: "isolated") with
        {
            Id = "dotnetcloud.files",
            Name = "Files",
            ManifestVersion = 2,
            HandlesUserErasure = true,
        };

        var result = _loader.Validate(manifest, "dotnetcloud.files");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Required modules must not declare a dataStore")));
    }

    [TestMethod]
    public void LoadAndValidate_V1RemainsLenient()
    {
        var json = """
            {
              // v1 allows comments
              "Id": "test.module",
              "Name": "Test Module",
              "Version": "1.0.0",
              "minCoreVersion": "0.7.0",
            }
            """;

        var result = _loader.LoadAndValidate(WriteManifest(json), "test.module");

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.AreEqual(1, result.Manifest!.ManifestVersion);
        Assert.AreEqual(ModuleDataStoreMode.None, result.Manifest.EffectiveDataStoreMode);
    }

    private static ModuleManifestData CreateManifest(
        string? dataStore,
        bool? handlesUserErasure = true,
        IReadOnlyList<string>? scopes = null) => new()
        {
            ManifestVersion = 2,
            Id = "org.example.customapp",
            Name = "Custom App",
            Version = "1.2.0",
            ModuleApiVersion = 1,
            DataStore = dataStore,
            HandlesUserErasure = handlesUserErasure,
            UserConsentScopes = scopes ?? [],
        };

    [TestMethod]
    public void LoadAndValidate_UnknownConsentScope_IsRejected()
    {
        var json = ValidV2.Replace(
            "\"userConsentScopes\": [\"files:read\", \"calendar:write\", \"notifications:send\"]",
            "\"userConsentScopes\": [\"files:read\", \"made-up:read\", \"notifications:send\"]");

        var result = _loader.LoadAndValidate(WriteManifest(json), "org.example.customapp");

        Assert.IsFalse(result.IsValid, "A scope the core cannot describe is a scope a user cannot approve.");
        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("made-up:read", StringComparison.Ordinal)),
            string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void LoadAndValidate_IdentityScopeDeclaredAsConsent_IsRejected()
    {
        var json = ValidV2.Replace(
            "\"userConsentScopes\": [\"files:read\", \"calendar:write\", \"notifications:send\"]",
            "\"userConsentScopes\": [\"files:read\", \"calendar:write\", \"notifications:send\", \"offline_access\"]");

        var result = _loader.LoadAndValidate(WriteManifest(json), "org.example.customapp");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("offline_access", StringComparison.Ordinal)),
            string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void LoadAndValidate_UserDataCapabilityWithoutItsScope_IsRejected()
    {
        // F18: a module that opts into consent must declare the scope for every user-data capability.
        var json = ValidV2.Replace(
            "\"userConsentScopes\": [\"files:read\", \"calendar:write\", \"notifications:send\"]",
            "\"userConsentScopes\": [\"files:read\", \"calendar:write\"]");

        var result = _loader.LoadAndValidate(WriteManifest(json), "org.example.customapp");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("notifications:send", StringComparison.Ordinal)),
            string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void LoadAndValidate_ModuleWithoutConsentScopes_IsNotSubjectToTheCapabilityRule()
    {
        // The transitional rule: a module that declares no scopes is unaffected, which is what keeps
        // required and bundled modules working until Track C migrates them.
        var json = ValidV2.Replace(
            "\"userConsentScopes\": [\"files:read\", \"calendar:write\", \"notifications:send\"],",
            string.Empty);

        var result = _loader.LoadAndValidate(WriteManifest(json), "org.example.customapp");

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }
}
