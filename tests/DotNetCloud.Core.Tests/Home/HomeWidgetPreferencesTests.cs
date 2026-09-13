using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Home;
using DotNetCloud.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DotNetCloud.Core.Tests.Home;

/// <summary>
/// Tests for Home-page widget preference persistence and style token handling.
/// </summary>
[TestClass]
public sealed class HomeWidgetPreferencesTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    [TestMethod]
    public void NewPreferences_NoStyleChosen_DefaultsToArtDepartment()
    {
        var preferences = new HomeWidgetPreferences();

        Assert.AreEqual(HomeWidgetStyles.ArtDepartment, preferences.Style);
        Assert.AreEqual(HomeWidgetStyles.ArtDepartment, HomeWidgetStyles.Normalize(null));
    }

    [TestMethod]
    public void AllStyleTokens_DistinctDisplayNamesAndDescriptions()
    {
        var names = HomeWidgetStyles.All.Select(HomeWidgetStyles.GetDisplayName).ToList();
        var descriptions = HomeWidgetStyles.All.Select(HomeWidgetStyles.GetDescription).ToList();

        Assert.AreEqual(3, HomeWidgetStyles.All.Count);
        Assert.AreEqual(3, names.Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(3, descriptions.Distinct(StringComparer.Ordinal).Count());
        Assert.IsFalse(names.Any(string.IsNullOrWhiteSpace));
        Assert.IsFalse(descriptions.Any(string.IsNullOrWhiteSpace));
    }

    [TestMethod]
    public void IsValid_UnknownToken_ReturnsFalse()
    {
        Assert.IsFalse(HomeWidgetStyles.IsValid("neon-chaos"));
        Assert.IsFalse(HomeWidgetStyles.IsValid(null));
        Assert.IsFalse(HomeWidgetStyles.IsValid("  "));
    }

    [TestMethod]
    public void Normalize_UnknownToken_ReturnsDefaultToken()
    {
        Assert.AreEqual(HomeWidgetStyles.DefaultToken, HomeWidgetStyles.Normalize("neon-chaos"));
        Assert.AreEqual(HomeWidgetStyles.HardCopy, HomeWidgetStyles.Normalize(HomeWidgetStyles.HardCopy));
    }

    [TestMethod]
    public void Serialize_DefaultPreferences_WritesCamelCaseAndDefaultStyle()
    {
        var json = HomeWidgetPreferencesSettings.Serialize(new HomeWidgetPreferences());

        StringAssert.Contains(json, "\"version\":1");
        StringAssert.Contains(json, $"\"style\":\"{HomeWidgetStyles.DefaultToken}\"");
        StringAssert.Contains(json, "\"items\"");
    }

    [TestMethod]
    public void Deserialize_RoundTrip_PreservesStyleOrderAndVisibility()
    {
        var original = new HomeWidgetPreferences
        {
            Style = HomeWidgetStyles.HardCopy,
            Items =
            [
                new HomeWidgetPreferenceItem { ModuleId = "dotnetcloud.notes", Visible = true },
                new HomeWidgetPreferenceItem { ModuleId = "dotnetcloud.files", Visible = false },
            ],
        };

        var result = HomeWidgetPreferencesSettings.Deserialize(
            HomeWidgetPreferencesSettings.Serialize(original));

        Assert.AreEqual(HomeWidgetStyles.HardCopy, result.Style);
        Assert.AreEqual(HomeWidgetPreferences.CurrentVersion, result.Version);
        Assert.AreEqual(2, result.Items.Count);
        Assert.AreEqual("dotnetcloud.notes", result.Items[0].ModuleId);
        Assert.IsTrue(result.Items[0].Visible);
        Assert.AreEqual("dotnetcloud.files", result.Items[1].ModuleId);
        Assert.IsFalse(result.Items[1].Visible);
    }

    [TestMethod]
    public void Deserialize_NullValue_ReturnsDefaults()
    {
        var result = HomeWidgetPreferencesSettings.Deserialize(null);

        Assert.AreEqual(HomeWidgetStyles.DefaultToken, result.Style);
        Assert.AreEqual(HomeWidgetPreferences.CurrentVersion, result.Version);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not json at all")]
    [DataRow("{ \"style\": ")]
    [DataRow("[]")]
    public void Deserialize_MalformedPayload_ReturnsDefaults(string payload)
    {
        var result = HomeWidgetPreferencesSettings.Deserialize(payload);

        Assert.AreEqual(HomeWidgetStyles.DefaultToken, result.Style);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public void Deserialize_UnknownStyleToken_FallsBackToDefaultToken()
    {
        var result = HomeWidgetPreferencesSettings.Deserialize(
            "{\"version\":1,\"style\":\"neon-chaos\",\"items\":[]}");

        Assert.AreEqual(HomeWidgetStyles.DefaultToken, result.Style);
    }

    [TestMethod]
    public void Deserialize_NewerSchemaVersion_ReturnsDefaults()
    {
        var result = HomeWidgetPreferencesSettings.Deserialize(
            "{\"version\":99,\"style\":\"hard-copy\",\"items\":[{\"moduleId\":\"a\",\"visible\":true}]}");

        Assert.AreEqual(HomeWidgetStyles.DefaultToken, result.Style);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public void Deserialize_DuplicateAndBlankModuleIds_KeepsFirstEntryOnly()
    {
        var result = HomeWidgetPreferencesSettings.Deserialize(
            "{\"version\":1,\"style\":\"art-department\",\"items\":[" +
            "{\"moduleId\":\"a\",\"visible\":false}," +
            "{\"moduleId\":\"A\",\"visible\":true}," +
            "{\"moduleId\":\"   \"}]}");

        Assert.AreEqual(1, result.Items.Count);
        Assert.AreEqual("a", result.Items[0].ModuleId);
        Assert.IsFalse(result.Items[0].Visible);
    }

    [TestMethod]
    public async Task LoadAsync_NoStoredSetting_ReturnsDefaults()
    {
        var settings = new Mock<IUserSettingsService>(MockBehavior.Strict);
        settings
            .Setup(s => s.GetSettingAsync(
                UserId,
                HomeWidgetPreferencesSettings.SettingsModule,
                HomeWidgetPreferencesSettings.PreferencesKey))
            .ReturnsAsync((UserSettingDto?)null);

        var result = await HomeWidgetPreferencesSettings.LoadAsync(settings.Object, UserId);

        Assert.AreEqual(HomeWidgetStyles.DefaultToken, result.Style);
        Assert.AreEqual(HomeWidgetPreferences.CurrentVersion, result.Version);
    }

    [TestMethod]
    public async Task SaveAsync_ValidPreferences_UpsertsUnderHomeWidgetsModule()
    {
        UpsertUserSettingDto? captured = null;
        var settings = new Mock<IUserSettingsService>(MockBehavior.Strict);
        settings
            .Setup(s => s.UpsertSettingAsync(
                UserId,
                HomeWidgetPreferencesSettings.SettingsModule,
                HomeWidgetPreferencesSettings.PreferencesKey,
                It.IsAny<UpsertUserSettingDto>()))
            .Callback<Guid, string, string, UpsertUserSettingDto>((_, _, _, dto) => captured = dto)
            .ReturnsAsync(new UserSettingDto { Value = "{}" });

        await HomeWidgetPreferencesSettings.SaveAsync(
            settings.Object,
            UserId,
            new HomeWidgetPreferences { Style = HomeWidgetStyles.StrictlyBusiness });

        Assert.IsNotNull(captured);
        StringAssert.Contains(captured!.Value, HomeWidgetStyles.StrictlyBusiness);
        Assert.AreEqual("Home-page widget layout (style, order, visibility)", captured.Description);
    }

    [TestMethod]
    public async Task SaveThenLoad_RoundTripsThroughSettingsService()
    {
        string? stored = null;
        var settings = new Mock<IUserSettingsService>(MockBehavior.Strict);
        settings
            .Setup(s => s.UpsertSettingAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<UpsertUserSettingDto>()))
            .Callback<Guid, string, string, UpsertUserSettingDto>((_, _, _, dto) => stored = dto.Value)
            .ReturnsAsync(new UserSettingDto { Value = "{}" });
        settings
            .Setup(s => s.GetSettingAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(() => stored is null ? null : new UserSettingDto { Value = stored });

        await HomeWidgetPreferencesSettings.SaveAsync(
            settings.Object,
            UserId,
            new HomeWidgetPreferences
            {
                Style = HomeWidgetStyles.HardCopy,
                Items = [new HomeWidgetPreferenceItem { ModuleId = "dotnetcloud.ai", Visible = false }],
            });

        var loaded = await HomeWidgetPreferencesSettings.LoadAsync(settings.Object, UserId);

        Assert.AreEqual(HomeWidgetStyles.HardCopy, loaded.Style);
        Assert.AreEqual(1, loaded.Items.Count);
        Assert.AreEqual("dotnetcloud.ai", loaded.Items[0].ModuleId);
        Assert.IsFalse(loaded.Items[0].Visible);
    }
}
