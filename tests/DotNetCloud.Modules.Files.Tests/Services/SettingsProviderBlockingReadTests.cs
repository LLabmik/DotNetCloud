using System.Diagnostics;
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
/// Regression tests for the settings providers' synchronous <c>Current</c> reads.
/// </summary>
/// <remarks>
/// <para>
/// <c>Current</c> exists for callers that cannot await (DI HTTP-handler factories, WOPI plumbing), but it
/// used to be <c>GetAsync().GetAwaiter().GetResult()</c> with no upper bound. Every Files page load reads
/// it from <c>FileBrowser.OnInitializedAsync</c>, so a handful of concurrent or retried loads blocked
/// enough ThreadPool threads on the provider's <c>SemaphoreSlim</c> that the gate holder's continuation
/// could never be scheduled — every subsequent Files page load then hung forever (a memory dump showed six
/// blocked <c>FileBrowser.OnInitializedAsync</c> state machines waiting inside
/// <c>CollaboraSettingsProvider.GetAsync</c>). The wait is now bounded, so a stuck refresh degrades to the
/// last known value instead of hanging the process.
/// </para>
/// <para>
/// These tests hold the provider's gate from an in-flight refresh and assert that <c>Current</c> still
/// returns (with the configured baseline) instead of blocking.
/// </para>
/// </remarks>
[TestClass]
public class SettingsProviderBlockingReadTests
{
    private static readonly TimeSpan BlockingTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>A settings service that signals when its read starts and then blocks until released.</summary>
    private static Mock<IAdminSettingsService> BlockingSettingsService(SemaphoreSlim entered, SemaphoreSlim release)
    {
        var service = new Mock<IAdminSettingsService>();
        service.Setup(s => s.ListSettingsAsync(It.IsAny<string?>()))
            .Returns(async () =>
            {
                entered.Release();
                await release.WaitAsync();
                return new List<SystemSettingDto>();
            });
        return service;
    }

    private static ServiceProvider BuildScopeFactory(IAdminSettingsService settingsService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => settingsService);

        // validateScopes: true, matching production — resolving the scoped settings service from the root
        // provider would fail instead of silently degrading to the configuration baseline.
        return services.BuildServiceProvider(validateScopes: true);
    }

    [TestMethod]
    public async Task CollaboraProvider_Current_WhileRefreshHoldsTheGate_ReturnsBaselineInsteadOfHanging()
    {
        using var entered = new SemaphoreSlim(0);
        using var release = new SemaphoreSlim(0);
        using var root = BuildScopeFactory(BlockingSettingsService(entered, release).Object);

        var baseline = new CollaboraOptions { ServerUrl = "https://baseline.example" };
        var provider = new CollaboraSettingsProvider(
            OptionsHelper.Create(baseline),
            root.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CollaboraSettingsProvider>.Instance,
            TimeSpan.Zero,
            BlockingTimeout);

        // Start a refresh and wait until it is inside the settings read, holding the provider's gate.
        var inFlight = provider.GetAsync();
        Assert.IsTrue(await entered.WaitAsync(TimeSpan.FromSeconds(5)), "the settings read never started");

        var stopwatch = Stopwatch.StartNew();
        var current = provider.Current;
        stopwatch.Stop();

        Assert.AreSame(baseline, current, "a refresh that does not complete in time must fall back to configuration");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Current blocked for {stopwatch.Elapsed}; it must stay bounded");

        release.Release();
        await inFlight;
    }

    [TestMethod]
    public async Task VersioningProvider_Current_WhileRefreshHoldsTheGate_ReturnsBaselineInsteadOfHanging()
    {
        using var entered = new SemaphoreSlim(0);
        using var release = new SemaphoreSlim(0);
        using var root = BuildScopeFactory(BlockingSettingsService(entered, release).Object);

        var baseline = new VersionRetentionOptions { Enabled = true, MaxVersionCount = 50 };
        var provider = new FileVersioningSettingsProvider(
            OptionsHelper.Create(baseline),
            root.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<FileVersioningSettingsProvider>.Instance,
            TimeSpan.Zero,
            BlockingTimeout);

        var inFlight = provider.GetAsync();
        Assert.IsTrue(await entered.WaitAsync(TimeSpan.FromSeconds(5)), "the settings read never started");

        var stopwatch = Stopwatch.StartNew();
        var current = provider.Current;
        stopwatch.Stop();

        Assert.AreSame(baseline, current, "a refresh that does not complete in time must fall back to configuration");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Current blocked for {stopwatch.Elapsed}; it must stay bounded");

        release.Release();
        await inFlight;
    }

    [TestMethod]
    public async Task CollaboraProvider_GetAsync_WhenRefreshIsCancelled_DoesNotCacheTheFallback()
    {
        using var entered = new SemaphoreSlim(0);
        using var release = new SemaphoreSlim(0);
        var settings = BlockingSettingsService(entered, release);
        using var root = BuildScopeFactory(settings.Object);

        var baseline = new CollaboraOptions { ServerUrl = "https://baseline.example" };
        var provider = new CollaboraSettingsProvider(
            OptionsHelper.Create(baseline),
            root.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CollaboraSettingsProvider>.Instance,
            TimeSpan.FromMinutes(5),
            BlockingTimeout);

        using var cancellation = new CancellationTokenSource();
        var cancelled = provider.GetAsync(cancellation.Token);
        Assert.IsTrue(await entered.WaitAsync(TimeSpan.FromSeconds(5)), "the settings read never started");
        cancellation.Cancel();

        release.Release();
        await cancelled;

        // The cache window is long, so a cached fallback would hide every later read for minutes.
        var refresh = provider.GetAsync();
        Assert.IsTrue(await entered.WaitAsync(TimeSpan.FromSeconds(5)),
            "the second read must hit the settings store again");
        release.Release();
        var refreshed = await refresh;

        Assert.AreNotSame(baseline, refreshed, "the resolved value is a fresh copy of the baseline");
        settings.Verify(s => s.ListSettingsAsync(It.IsAny<string?>()), Times.Exactly(2),
            "a cancelled read must not be cached — the next call has to hit the settings store again");
    }
}
