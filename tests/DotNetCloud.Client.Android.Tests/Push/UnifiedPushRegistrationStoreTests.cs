using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Android.Push.Tests;

/// <summary>
/// Covers persistence of UnifiedPush registrations: one entry per saved server connection
/// (plan §9.7), token lookup for distributor callbacks, and robustness to corrupt state.
/// </summary>
[TestClass]
public sealed class UnifiedPushRegistrationStoreTests
{
    private const string ServerUrl = "https://cloud.example.com";

    private static UnifiedPushRegistration Registration() => new()
    {
        ServerBaseUrl = ServerUrl,
        Token = UnifiedPushProtocol.CreateToken(),
        Endpoint = "https://cloud.example.com/push/upTopic",
        State = UnifiedPushRegistrationState.Registered,
    };

    private static UnifiedPushRegistrationStore NewStore(FakePreferences preferences) =>
        new(preferences, NullLogger<UnifiedPushRegistrationStore>.Instance);

    [TestMethod]
    public void Save_RoundTripsPerServerConnectionAndToken()
    {
        // Arrange
        var store = NewStore(new FakePreferences());
        var registration = Registration();

        // Act
        store.Save(registration);

        // Assert: the URL matches regardless of a trailing slash or casing, so a tap can never be
        // attributed to the wrong connection.
        Assert.AreEqual(registration.Endpoint, store.Get("https://CLOUD.example.com/")?.Endpoint);
        Assert.AreEqual(ServerUrl, store.FindByToken(registration.Token)?.ServerBaseUrl);
        Assert.IsNull(store.FindByToken("some-other-token"));
        Assert.AreEqual(1, store.GetAll().Count);
    }

    [TestMethod]
    public void Save_SameConnectionTwice_ReplacesInsteadOfDuplicating()
    {
        // Arrange
        var store = NewStore(new FakePreferences());
        store.Save(Registration());

        // Act
        store.Save(Registration() with { Endpoint = "https://cloud.example.com/push/upOther" });

        // Assert
        Assert.AreEqual(1, store.GetAll().Count);
        Assert.AreEqual("https://cloud.example.com/push/upOther", store.Get(ServerUrl)?.Endpoint);
    }

    [TestMethod]
    public void Remove_DropsTheConnection()
    {
        // Arrange
        var store = NewStore(new FakePreferences());
        store.Save(Registration());

        // Act
        store.Remove(ServerUrl);

        // Assert
        Assert.IsNull(store.Get(ServerUrl));
        Assert.IsEmpty(store.GetAll());
    }

    [TestMethod]
    public void Remove_UnknownConnection_IsANoOp()
    {
        // Arrange
        var store = NewStore(new FakePreferences());
        store.Save(Registration());

        // Act
        store.Remove("https://other.example.com");

        // Assert
        Assert.AreEqual(1, store.GetAll().Count);
    }

    [TestMethod]
    public void GetAll_CorruptPersistedJson_StartsEmptyInsteadOfThrowing()
    {
        // Arrange
        var preferences = new FakePreferences();
        preferences.Set("up_registrations_v1", "{ not json");
        var store = NewStore(preferences);

        // Act + Assert
        Assert.IsEmpty(store.GetAll());
        Assert.IsNull(store.Get(ServerUrl));
    }

    [TestMethod]
    public void SelectedDistributorPackage_RoundTripsAndClears()
    {
        // Arrange
        var store = NewStore(new FakePreferences());

        // Act + Assert
        Assert.IsNull(store.SelectedDistributorPackage);

        store.SelectedDistributorPackage = "io.heckel.ntfy";
        Assert.AreEqual("io.heckel.ntfy", store.SelectedDistributorPackage);

        store.SelectedDistributorPackage = null;
        Assert.IsNull(store.SelectedDistributorPackage);
    }

    [TestMethod]
    public void Save_WithoutTokenOrServer_IsIgnored()
    {
        // Arrange
        var store = NewStore(new FakePreferences());

        // Act
        store.Save(new UnifiedPushRegistration { ServerBaseUrl = ServerUrl, Token = string.Empty });
        store.Save(Registration() with { ServerBaseUrl = string.Empty });

        // Assert
        Assert.IsEmpty(store.GetAll());
    }

    private sealed class FakePreferences : IAppPreferences
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        public T Get<T>(string key, T defaultValue) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

        public void Set<T>(string key, T value) => _values[key] = value!;
    }
}
