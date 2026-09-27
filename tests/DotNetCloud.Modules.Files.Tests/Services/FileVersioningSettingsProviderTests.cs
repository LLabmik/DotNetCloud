using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using OptionsHelper = Microsoft.Extensions.Options.Options;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests <see cref="FileVersioningSettingsProvider"/>: it must read the administrator's
/// <c>VersionRetention:*</c> rows on top of the configuration baseline, degrade to the baseline when
/// the settings store is unavailable, and resolve the (scoped) settings service correctly — a
/// singleton provider that grabbed it from the root provider would fail under scope validation and
/// silently fall back to configuration, which is exactly the "my settings are ignored" symptom.
/// </summary>
[TestClass]
public class FileVersioningSettingsProviderTests
{
    private static FileVersioningSettingsProvider CreateProvider(
        VersionRetentionOptions baseline,
        IAdminSettingsService? settingsService = null)
    {
        var services = new ServiceCollection();
        if (settingsService is not null)
        {
            services.AddScoped(_ => settingsService);
        }

        // validateScopes: true so resolving the scoped settings service from the root provider would
        // fail loudly instead of quietly returning no rows.
        var root = services.BuildServiceProvider(validateScopes: true);

        return new FileVersioningSettingsProvider(
            OptionsHelper.Create(baseline),
            root.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<FileVersioningSettingsProvider>.Instance,
            TimeSpan.Zero);
    }

    private static IAdminSettingsService CreateSettingsService(params (string Key, string Value)[] rows)
    {
        var service = new Mock<IAdminSettingsService>();
        service.Setup(s => s.ListSettingsAsync(It.IsAny<string?>()))
            .ReturnsAsync(rows
                .Select(r => new SystemSettingDto
                {
                    Module = FileVersioningAdminSettings.ModuleId,
                    Key = r.Key,
                    Value = r.Value
                })
                .ToList());
        return service.Object;
    }

    [TestMethod]
    public async Task GetAsync_AdminRowsPresent_OverrideTheConfigurationBaseline()
    {
        var baseline = new VersionRetentionOptions { Enabled = true, MaxVersionCount = 50, RetentionDays = 0 };
        var provider = CreateProvider(
            baseline,
            settingsService: CreateSettingsService(
                ("VersionRetention:Enabled", "false"),
                ("VersionRetention:MaxNumber", "12"),
                ("VersionRetention:MaxDays", "30")));

        var effective = await provider.GetAsync();

        Assert.IsFalse(effective.Enabled);
        Assert.AreEqual(12, effective.MaxVersionCount);
        Assert.AreEqual(30, effective.RetentionDays);

        // The shared configuration instance is left untouched.
        Assert.IsTrue(baseline.Enabled);
        Assert.AreEqual(50, baseline.MaxVersionCount);
        Assert.AreEqual(0, baseline.RetentionDays);
    }

    [TestMethod]
    public async Task GetAsync_NoSettingsServiceRegistered_FallsBackToConfiguration()
    {
        var baseline = new VersionRetentionOptions { Enabled = true, MaxVersionCount = 50, RetentionDays = 14 };
        var provider = CreateProvider(baseline);

        var effective = await provider.GetAsync();

        Assert.IsTrue(effective.Enabled);
        Assert.AreEqual(50, effective.MaxVersionCount);
        Assert.AreEqual(14, effective.RetentionDays);
    }

    [TestMethod]
    public async Task GetAsync_SettingsReadThrows_FallsBackToConfiguration()
    {
        var service = new Mock<IAdminSettingsService>();
        service.Setup(s => s.ListSettingsAsync(It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("settings store unavailable"));

        var baseline = new VersionRetentionOptions { MaxVersionCount = 25 };
        var provider = CreateProvider(baseline, settingsService: service.Object);

        var effective = await provider.GetAsync();

        Assert.AreEqual(25, effective.MaxVersionCount);
    }

    [TestMethod]
    public async Task GetAsync_WithCaching_ReReadsAfterInvalidate()
    {
        var rows = new List<(string Key, string Value)> { ("VersionRetention:MaxNumber", "7") };
        var service = new Mock<IAdminSettingsService>();
        service.Setup(s => s.ListSettingsAsync(It.IsAny<string?>()))
            .ReturnsAsync(() => rows
                .Select(r => new SystemSettingDto
                {
                    Module = FileVersioningAdminSettings.ModuleId,
                    Key = r.Key,
                    Value = r.Value
                })
                .ToList());

        var services = new ServiceCollection();
        services.AddScoped(_ => service.Object);
        var root = services.BuildServiceProvider(validateScopes: true);

        var provider = new FileVersioningSettingsProvider(
            OptionsHelper.Create(new VersionRetentionOptions()),
            root.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<FileVersioningSettingsProvider>.Instance);

        Assert.AreEqual(7, (await provider.GetAsync()).MaxVersionCount);

        // Within the cache window the stored value is not re-read...
        rows[0] = ("VersionRetention:MaxNumber", "9");
        Assert.AreEqual(7, (await provider.GetAsync()).MaxVersionCount);

        // ...but an admin save invalidates the cache, so the page and the module see the new value.
        provider.Invalidate();
        Assert.AreEqual(9, (await provider.GetAsync()).MaxVersionCount);
    }
}
