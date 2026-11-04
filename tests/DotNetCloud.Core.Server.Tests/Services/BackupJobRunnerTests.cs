using System.Diagnostics;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the background backup runner that lets the Admin UI start a large backup without
/// holding an HTTP request open (the browser's default timeout is 100 seconds).
/// </summary>
[TestClass]
public class BackupJobRunnerTests
{
    private static (BackupJobRunner Runner, ServiceProvider Provider) CreateRunner(
        Func<string?, BackupOptions?, CancellationToken, Task<BackupResult>> handler)
    {
        var services = new ServiceCollection();
        services.AddScoped<IBackupService>(_ => new FakeBackupService(handler));
        var provider = services.BuildServiceProvider();
        var runner = new BackupJobRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BackupJobRunner>.Instance);
        return (runner, provider);
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.ElapsedMilliseconds > timeoutMs)
                Assert.Fail("Timed out waiting for the backup runner state to change.");

            await Task.Delay(20);
        }
    }

    [TestMethod]
    public async Task TryStart_CompletesJob_ReportsSuccessAndMetadata()
    {
        var completion = new TaskCompletionSource<BackupResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (runner, provider) = CreateRunner((_, _, _) => completion.Task);
        await using var providerScope = provider;

        Assert.IsTrue(runner.TryStart(new BackupOptions(), "/tmp/backup.zip", out var error));
        Assert.IsNull(error);
        Assert.IsTrue(runner.GetStatus().IsRunning);

        completion.SetResult(new BackupResult
        {
            Success = true,
            FilePath = "/tmp/backup.zip",
            FileCount = 42,
            SizeBytes = 1234,
        });

        await WaitForAsync(() => !runner.GetStatus().IsRunning);

        var status = runner.GetStatus();
        Assert.IsTrue(status.LastBackupSuccess);
        Assert.AreEqual("/tmp/backup.zip", status.LastBackupPath);
        Assert.AreEqual(42, status.LastBackupFileCount);
        Assert.AreEqual(1234, status.LastBackupSizeBytes);
        Assert.AreEqual("completed", status.Phase);
        Assert.IsNull(status.LastError);
    }

    [TestMethod]
    public async Task TryStart_WhileRunning_ReturnsFalse()
    {
        var completion = new TaskCompletionSource<BackupResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (runner, provider) = CreateRunner((_, _, _) => completion.Task);
        await using var providerScope = provider;

        Assert.IsTrue(runner.TryStart(new BackupOptions(), null, out _));

        Assert.IsFalse(runner.TryStart(new BackupOptions(), null, out var error));
        Assert.AreEqual("A backup is already running.", error);

        completion.SetResult(new BackupResult { Success = true });
        await WaitForAsync(() => !runner.GetStatus().IsRunning);
    }

    [TestMethod]
    public void TryCancel_NoJobRunning_ReturnsFalse()
    {
        var (runner, provider) = CreateRunner((_, _, _) => Task.FromResult(new BackupResult { Success = true }));
        provider.Dispose();

        Assert.IsFalse(runner.TryCancel());
    }

    [TestMethod]
    public async Task TryCancel_RunningJob_ReportsCancelled()
    {
        var (runner, provider) = CreateRunner(async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new BackupResult { Success = true };
        });
        await using var providerScope = provider;

        Assert.IsTrue(runner.TryStart(new BackupOptions(), null, out _));
        await WaitForAsync(() => runner.GetStatus().IsRunning);

        Assert.IsTrue(runner.TryCancel());
        Assert.IsTrue(runner.GetStatus().CancelRequested);

        await WaitForAsync(() => !runner.GetStatus().IsRunning);

        var status = runner.GetStatus();
        Assert.AreEqual("cancelled", status.Phase);
        Assert.IsFalse(status.LastBackupSuccess == true);
        Assert.AreEqual("Backup cancelled.", status.LastError);
    }

    [TestMethod]
    public async Task TryStart_JobThrows_ReportsFailureWithMessage()
    {
        var (runner, provider) = CreateRunner((_, _, _) =>
            Task.FromException<BackupResult>(new InvalidOperationException("boom")));
        await using var providerScope = provider;

        Assert.IsTrue(runner.TryStart(new BackupOptions(), null, out _));
        await WaitForAsync(() => !runner.GetStatus().IsRunning);

        var status = runner.GetStatus();
        Assert.AreEqual("failed", status.Phase);
        Assert.AreEqual("boom", status.LastError);
        Assert.IsFalse(status.LastBackupSuccess == true);
    }

    private sealed class FakeBackupService(
        Func<string?, BackupOptions?, CancellationToken, Task<BackupResult>> handler) : IBackupService
    {
        public Task<BackupResult> CreateBackupAsync(
            string? outputPath = null,
            BackupOptions? options = null,
            CancellationToken cancellationToken = default)
            => handler(outputPath, options, cancellationToken);

        public Task<BackupResult> RestoreBackupAsync(
            string filePath,
            RestoreOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<BackupStatusInfo> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BackupStatusInfo());
    }
}
