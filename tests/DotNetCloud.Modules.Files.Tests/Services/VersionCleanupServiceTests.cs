using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using OptionsHelper = Microsoft.Extensions.Options.Options;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// The scheduled cleanup service is a thin wrapper: it resolves
/// <see cref="IVersionRetentionService"/> from a scope and runs it. The policy itself is covered by
/// <see cref="VersionRetentionServiceTests"/>.
/// </summary>
[TestClass]
public class VersionCleanupServiceTests
{
    private static (VersionCleanupService Service, Mock<IVersionRetentionService> Retention, List<string> RecordedRuns) CreateService()
    {
        var retention = new Mock<IVersionRetentionService>();
        retention.Setup(r => r.RunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionRetentionSweepResult(true, 0, 0));

        var recordedRuns = new List<string>();
        var tracker = new Mock<IBackgroundServiceTracker>();
        tracker
            .Setup(t => t.RecordRun(
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Callback<string, DateTimeOffset, TimeSpan, bool, string?>(
                (_, _, _, success, message) => recordedRuns.Add($"success={success}; message={message}"));

        var services = new ServiceCollection();
        services.AddSingleton(retention.Object);
        var provider = services.BuildServiceProvider();

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var service = new VersionCleanupService(
            scopeFactory.Object,
            OptionsHelper.Create(new VersionRetentionOptions()),
            NullLogger<VersionCleanupService>.Instance,
            tracker.Object);

        return (service, retention, recordedRuns);
    }

    [TestMethod]
    public async Task CleanupAsync_AlwaysDelegatesToTheRetentionService()
    {
        var (service, retention, _) = CreateService();

        await service.CleanupAsync(CancellationToken.None);

        retention.Verify(r => r.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_InitialCycleRunsBeforeTheTimerStarts()
    {
        var (service, retention, recordedRuns) = CreateService();

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        // A cycle runs immediately on startup — before the 24h timer — so a deploy applies the
        // policy without waiting for the next scheduled pass.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (recordedRuns.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.AreEqual(1, recordedRuns.Count, string.Join(" | ", recordedRuns));
        Assert.AreEqual("success=True; message=", recordedRuns[0], string.Join(" | ", recordedRuns));
        retention.Verify(r => r.RunAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
