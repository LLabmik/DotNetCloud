using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChatSettingsProvider"/>: database precedence, configuration fallback,
/// default clamping, and the short-lived cache.
/// </summary>
[TestClass]
public class ChatSettingsProviderTests
{
    private const string Module = ChatSettingKeys.ModuleId;

    // ── Defaults ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSettingsAsync_WhenNothingConfigured_ReturnsDefaults()
    {
        var provider = CreateProvider(out _, [], new Dictionary<string, string?>());

        var settings = await provider.GetSettingsAsync();

        Assert.AreEqual(ChatSettings.DefaultMaxMessageLength, settings.MaxMessageLength);
        Assert.AreEqual(0, settings.MaxMessagesPerChannel);
        Assert.AreEqual(ChatSettings.DefaultMaxAttachmentsPerMessage, settings.MaxAttachmentsPerMessage);
        Assert.AreEqual(0, settings.MaxAttachmentsPerChannel);
        Assert.AreEqual(ChatSettings.DefaultMaxAttachmentSizeMb, settings.MaxAttachmentSizeMb);
        Assert.AreEqual(0, settings.MaxAttachmentStoragePerChannelMb);
        Assert.IsFalse(settings.RetentionEnabled);
        Assert.AreEqual(0, settings.MessageLifetimeDays);
        Assert.AreEqual(ChatRetentionMode.Archive, settings.RetentionMode);
        Assert.IsTrue(settings.ArchiveAttachments);
        Assert.AreEqual(ChatSettings.DefaultSweepIntervalMinutes, settings.SweepIntervalMinutes);
    }

    // ── Database precedence ─────────────────────────────────────────

    [TestMethod]
    public async Task GetSettingsAsync_WhenDatabaseRowsExist_TheyWinOverConfiguration()
    {
        var rows = new[]
        {
            Row(ChatSettingKeys.MaxMessageLength, "500"),
            Row(ChatSettingKeys.MaxMessagesPerChannel, "250"),
            Row(ChatSettingKeys.MaxAttachmentsPerMessage, "3"),
            Row(ChatSettingKeys.MaxAttachmentsPerChannel, "40"),
            Row(ChatSettingKeys.MaxAttachmentSizeMb, "25"),
            Row(ChatSettingKeys.MaxAttachmentStoragePerChannelMb, "800"),
            Row(ChatSettingKeys.RetentionEnabled, "true"),
            Row(ChatSettingKeys.MessageLifetimeDays, "90"),
            Row(ChatSettingKeys.RetentionMode, "Purge"),
            Row(ChatSettingKeys.ArchiveAttachments, "false"),
            Row(ChatSettingKeys.SweepIntervalMinutes, "15")
        };

        var provider = CreateProvider(out _, rows, new Dictionary<string, string?>
        {
            ["Chat:Limits:MaxMessageLength"] = "9999",
            ["Chat:Retention:Mode"] = "Archive"
        });

        var settings = await provider.GetSettingsAsync();

        Assert.AreEqual(500, settings.MaxMessageLength);
        Assert.AreEqual(250, settings.MaxMessagesPerChannel);
        Assert.AreEqual(3, settings.MaxAttachmentsPerMessage);
        Assert.AreEqual(40, settings.MaxAttachmentsPerChannel);
        Assert.AreEqual(25, settings.MaxAttachmentSizeMb);
        Assert.AreEqual(800, settings.MaxAttachmentStoragePerChannelMb);
        Assert.IsTrue(settings.RetentionEnabled);
        Assert.AreEqual(90, settings.MessageLifetimeDays);
        Assert.AreEqual(ChatRetentionMode.Purge, settings.RetentionMode);
        Assert.IsFalse(settings.ArchiveAttachments);
        Assert.AreEqual(15, settings.SweepIntervalMinutes);
    }

    [TestMethod]
    public async Task GetSettingsAsync_WhenDatabaseThrows_FallsBackToConfiguration()
    {
        var service = new Mock<IAdminSettingsService>();
        service.Setup(s => s.ListSettingsAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        var provider = CreateProvider(service, new Dictionary<string, string?>
        {
            ["Chat:Limits:MaxMessageLength"] = "750",
            ["Chat:Retention:Enabled"] = "true"
        });

        var settings = await provider.GetSettingsAsync();

        Assert.AreEqual(750, settings.MaxMessageLength);
        Assert.IsTrue(settings.RetentionEnabled);
    }

    [TestMethod]
    public async Task GetSettingsAsync_WhenServiceIsNotRegistered_UsesConfiguration()
    {
        var provider = new ChatSettingsProvider(
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["Chat:Limits:MaxMessagesPerChannel"] = "120",
                ["Chat:Retention:MessageLifetimeDays"] = "30",
                ["Chat:Retention:Mode"] = "Purge",
                ["Chat:Retention:ArchiveAttachments"] = "false"
            }),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<ChatSettingsProvider>.Instance);

        var settings = await provider.GetSettingsAsync();

        Assert.AreEqual(120, settings.MaxMessagesPerChannel);
        Assert.AreEqual(30, settings.MessageLifetimeDays);
        Assert.AreEqual(ChatRetentionMode.Purge, settings.RetentionMode);
        Assert.IsFalse(settings.ArchiveAttachments);
    }

    [TestMethod]
    public async Task GetSettingsAsync_IgnoresRowsOwnedByOtherModules()
    {
        var rows = new[]
        {
            new SystemSettingDto { Module = "dotnetcloud.chat.extra", Key = ChatSettingKeys.MaxMessageLength, Value = "1" },
            new SystemSettingDto { Module = Module, Key = ChatSettingKeys.MaxMessageLength, Value = "2222" }
        };

        var settings = await CreateProvider(out _, rows, new Dictionary<string, string?>())
            .GetSettingsAsync();

        Assert.AreEqual(2222, settings.MaxMessageLength, "Only rows owned by dotnetcloud.chat may be applied.");
    }

    // ── Clamping ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSettingsAsync_OutOfRangeValues_AreClamped()
    {
        var rows = new[]
        {
            Row(ChatSettingKeys.MaxMessageLength, "999999"),
            Row(ChatSettingKeys.MaxAttachmentsPerMessage, "5000"),
            Row(ChatSettingKeys.MaxAttachmentSizeMb, "9000"),
            Row(ChatSettingKeys.MessageLifetimeDays, "-10"),
            Row(ChatSettingKeys.MaxMessagesPerChannel, "-5"),
            Row(ChatSettingKeys.SweepIntervalMinutes, "99999")
        };

        var settings = await CreateProvider(out _, rows, new Dictionary<string, string?>())
            .GetSettingsAsync();

        Assert.AreEqual(ChatSettings.HardMaxMessageLength, settings.MaxMessageLength);
        Assert.AreEqual(100, settings.MaxAttachmentsPerMessage);
        Assert.AreEqual(ChatSettings.HardMaxAttachmentSizeMb, settings.MaxAttachmentSizeMb);
        Assert.AreEqual(0, settings.MessageLifetimeDays);
        Assert.AreEqual(0, settings.MaxMessagesPerChannel);
        Assert.AreEqual(1440, settings.SweepIntervalMinutes);
    }

    [TestMethod]
    public async Task GetSettingsAsync_UnrecognizedRetentionMode_DefaultsToArchive()
    {
        var rows = new[] { Row(ChatSettingKeys.RetentionMode, "Explode") };

        var settings = await CreateProvider(out _, rows, new Dictionary<string, string?>())
            .GetSettingsAsync();

        Assert.AreEqual(ChatRetentionMode.Archive, settings.RetentionMode);
    }

    // ── Cache ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSettingsAsync_WhenCachingEnabled_ReadsDatabaseOnce()
    {
        var service = new Mock<IAdminSettingsService>();
        service.Setup(s => s.ListSettingsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<SystemSettingDto> { Row(ChatSettingKeys.MaxMessageLength, "123") });

        var provider = CreateProvider(service);

        await provider.GetSettingsAsync();
        await provider.GetSettingsAsync();
        await provider.GetSettingsAsync();

        service.Verify(s => s.ListSettingsAsync(It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task Invalidate_ForcesTheNextReadToObserveNewValues()
    {
        var service = new Mock<IAdminSettingsService>();
        service.Setup(s => s.ListSettingsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<SystemSettingDto> { Row(ChatSettingKeys.MaxMessageLength, "111") });

        var provider = CreateProvider(service);
        Assert.AreEqual(111, (await provider.GetSettingsAsync()).MaxMessageLength);

        service.Setup(s => s.ListSettingsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<SystemSettingDto> { Row(ChatSettingKeys.MaxMessageLength, "222") });
        provider.Invalidate();

        Assert.AreEqual(222, (await provider.GetSettingsAsync()).MaxMessageLength);
    }

    [TestMethod]
    public async Task GetSettingsAsync_WhenCachingDisabled_ReReadsEveryTime()
    {
        var service = new Mock<IAdminSettingsService>();
        service.Setup(s => s.ListSettingsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<SystemSettingDto>());

        var provider = CreateProvider(service, cacheTtl: TimeSpan.Zero);

        await provider.GetSettingsAsync();
        await provider.GetSettingsAsync();

        service.Verify(s => s.ListSettingsAsync(It.IsAny<string>()), Times.Exactly(2));
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static SystemSettingDto Row(string key, string value) =>
        new() { Module = Module, Key = key, Value = value };

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ChatSettingsProvider CreateProvider(
        Mock<IAdminSettingsService> service,
        Dictionary<string, string?>? configuration = null,
        TimeSpan? cacheTtl = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(service.Object);
        return new ChatSettingsProvider(
            BuildConfiguration(configuration ?? []),
            services.BuildServiceProvider(),
            NullLogger<ChatSettingsProvider>.Instance,
            cacheTtl ?? TimeSpan.FromSeconds(30));
    }

    private static ChatSettingsProvider CreateProvider(
        out Mock<IAdminSettingsService> service,
        IReadOnlyList<SystemSettingDto> rows,
        Dictionary<string, string?> configuration,
        TimeSpan? cacheTtl = null)
    {
        service = new Mock<IAdminSettingsService>();
        service.Setup(s => s.ListSettingsAsync(It.IsAny<string>()))
            .ReturnsAsync(rows);
        return CreateProvider(service, configuration, cacheTtl);
    }
}
