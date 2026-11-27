using System.Collections.Concurrent;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.Services;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Sandbox;
using DotNetCloud.Core.Server.Supervisor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// Guards the two-loop shape of <see cref="ProcessSupervisor"/>: the status reconcile runs on its
/// own interval, independent of health probing, and a host shutdown ends both loops instead of
/// leaving one running as an orphaned task.
/// </summary>
[TestClass]
public class ProcessSupervisorLoopTests
{
    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Long enough that no health tick can occur during a test.</summary>
    private static readonly TimeSpan SlowHealthInterval = TimeSpan.FromMinutes(30);

    private string _modulesDirectory = null!;
    private readonly List<IDisposable> _disposables = [];

    [TestInitialize]
    public void Setup()
    {
        _modulesDirectory = Path.Combine(
            Path.GetTempPath(), "dnc-supervisor-tests", Guid.NewGuid().ToString("N"), "modules");
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _disposables.Clear();

        var root = Directory.GetParent(_modulesDirectory)?.FullName;
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task StoppingTheHostEndsBothLoopsAndLeavesNoOrphanedTask()
    {
        var log = new SupervisorLogCapture();
        var scopes = new CountingScopeFactory(CreateScopeFactory());
        var supervisor = CreateSupervisor(log, scopes);

        await supervisor.StartAsync(CancellationToken.None);
        await WaitUntilAsync(
            () => log.CountContaining("Health monitor started") == 1
                && log.CountContaining("Status reconcile loop started") == 1,
            "both loops to report started");

        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await supervisor.StopAsync(stopTimeout.Token);

        Assert.IsNotNull(supervisor.ExecuteTask);
        Assert.IsTrue(
            supervisor.ExecuteTask.IsCompleted,
            "ExecuteAsync must return once the host stops; otherwise a loop is still running.");
        Assert.AreEqual(1, log.CountContaining("Health monitor stopped"));
        Assert.AreEqual(1, log.CountContaining("Status reconcile loop stopped"));

        supervisor.Dispose();
    }

    [TestMethod]
    public async Task TheReconcileLoopTicksOnItsOwnIntervalWhileHealthProbingIsSlow()
    {
        // The whole point of the phase: the reconcile cadence must not be gated by
        // HealthCheckInterval. Each pass opens a scope to read the status table.
        var log = new SupervisorLogCapture();
        var scopes = new CountingScopeFactory(CreateScopeFactory());
        var supervisor = CreateSupervisor(log, scopes);

        try
        {
            await supervisor.StartAsync(CancellationToken.None);
            await WaitUntilAsync(
                () => scopes.Count >= 3,
                $"three status reconcile passes at {ReconcileInterval.TotalMilliseconds} ms");
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await supervisor.StopAsync(stopTimeout.Token);
            supervisor.Dispose();
        }

        Assert.IsTrue(
            scopes.Count >= 3,
            $"Expected at least 3 status queries within the wait, saw {scopes.Count}.");
    }

    private ProcessSupervisor CreateSupervisor(SupervisorLogCapture log, CountingScopeFactory scopes)
    {
        var options = new ProcessSupervisorOptions
        {
            ModulesDirectory = _modulesDirectory,
            HealthCheckInterval = SlowHealthInterval,
            StatusReconcileInterval = ReconcileInterval,
        };

        var optionsAccessor = Options.Create(options);
        var credentials = new ModuleCredentialStore();
        var assertions = new ModuleAssertionKeyRing();
        _disposables.Add(assertions);

        var provisioning = new Mock<IModuleDataStoreProvisioningService>();

        return new ProcessSupervisor(
            log,
            optionsAccessor,
            new ModuleDiscoveryService(NullLogger<ModuleDiscoveryService>.Instance, optionsAccessor),
            new ModuleManifestLoader(NullLogger<ModuleManifestLoader>.Instance),
            new ModuleManifestRegistry(),
            new GrpcChannelManager(NullLogger<GrpcChannelManager>.Instance, credentials),
            new ResourceLimiter(NullLogger<ResourceLimiter>.Instance),
            scopes,
            new ModuleSchemaService([], NullLogger<ModuleSchemaService>.Instance),
            credentials,
            assertions,
            provisioning.Object,
            new CoreDatabaseOptions("Server=unused;Database=unused", DatabaseProvider.SqlServer),
            new ModuleQuotaResolver(scopes, Options.Create(new ModuleQuotasOptions()), optionsAccessor),
            new ModuleEndpointAllocator(
                ModuleTransportKind.Tcp, unixSocketDirectory: null, "dotnetcloud", options, Path.GetTempPath()));
    }

    /// <summary>
    /// A scope factory over a real (empty) InMemory context, so the status query is exercised
    /// rather than short-circuited by a failure.
    /// </summary>
    private IServiceScopeFactory CreateScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase($"ProcessSupervisorLoopTests_{Guid.NewGuid():N}"));
        services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());

        var provider = services.BuildServiceProvider();
        _disposables.Add(provider);

        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string expectation)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"Timed out waiting for {expectation}.");
    }

    /// <summary>Counts <see cref="IServiceScopeFactory.CreateScope"/> calls: one per reconcile pass.</summary>
    private sealed class CountingScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _count);
            return inner.CreateScope();
        }
    }

    /// <summary>Captures the supervisor's own log lines.</summary>
    private sealed class SupervisorLogCapture : ILogger<ProcessSupervisor>
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public int CountContaining(string text) =>
            _messages.Count(message => message.Contains(text, StringComparison.Ordinal));

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            _messages.Enqueue(formatter(state, exception));
        }
    }
}
