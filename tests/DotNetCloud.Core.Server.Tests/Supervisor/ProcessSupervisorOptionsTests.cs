using System.Collections.Concurrent;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// Guards the configuration surface of the status reconcile interval: the binding default, the
/// 250 ms floor and the startup line. The floor matters because the reconcile loop queries the
/// module status table on every tick.
/// </summary>
[TestClass]
public class ProcessSupervisorOptionsTests
{
    private const string StatusReconcileOption = "ProcessSupervisor:StatusReconcileInterval";

    [TestMethod]
    public void DefaultsKeepProductionAtThirtySeconds()
    {
        var options = new ProcessSupervisorOptions();

        Assert.AreEqual(TimeSpan.FromSeconds(30), options.StatusReconcileInterval);
        Assert.AreEqual(
            options.StatusReconcileInterval,
            options.EffectiveStatusReconcileInterval,
            "The default must not need the floor applied.");

        Assert.AreEqual(
            TimeSpan.FromSeconds(15),
            options.HealthCheckInterval,
            "Health probing keeps its own cadence: only the reconcile interval became configurable.");
    }

    [TestMethod]
    public void ConfigurationBindsTheInterval()
    {
        using var provider = BuildProvider(
            Configuration("00:00:05"),
            Environments.Production,
            configureOptions: null);

        var options = provider.GetRequiredService<IOptions<ProcessSupervisorOptions>>().Value;

        Assert.AreEqual(TimeSpan.FromSeconds(5), options.StatusReconcileInterval);
        Assert.AreEqual(TimeSpan.FromSeconds(5), options.EffectiveStatusReconcileInterval);
    }

    [TestMethod]
    public void TheConfigureOptionsOverloadCannotBypassTheFloor()
    {
        using var provider = BuildProvider(
            configuration: null,
            Environments.Development,
            configureOptions: options => options.StatusReconcileInterval = TimeSpan.FromMilliseconds(10));

        var bound = provider.GetRequiredService<IOptions<ProcessSupervisorOptions>>().Value;

        Assert.AreEqual(
            ProcessSupervisorOptions.MinimumStatusReconcileInterval,
            bound.StatusReconcileInterval,
            "The clamp lives in the options pipeline, so the configureOptions overload cannot skip it.");
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), bound.EffectiveStatusReconcileInterval);
    }

    [TestMethod]
    public void ConfigurationCannotBypassTheFloor()
    {
        using var provider = BuildProvider(
            Configuration("00:00:00.010"),
            Environments.Production,
            configureOptions: null);

        var bound = provider.GetRequiredService<IOptions<ProcessSupervisorOptions>>().Value;

        Assert.AreEqual(TimeSpan.FromMilliseconds(250), bound.StatusReconcileInterval);
    }

    [TestMethod]
    public void ANegativeIntervalIsRaisedToTheFloor()
    {
        using var provider = BuildProvider(
            configuration: null,
            Environments.Development,
            configureOptions: options => options.StatusReconcileInterval = TimeSpan.FromSeconds(-1));

        var bound = provider.GetRequiredService<IOptions<ProcessSupervisorOptions>>().Value;

        Assert.AreEqual(TimeSpan.FromMilliseconds(250), bound.StatusReconcileInterval);
    }

    [TestMethod]
    public void TheEffectiveIntervalIsLoggedOnceAtStartup()
    {
        var logger = new CapturingLoggerProvider();

        using var provider = BuildProvider(
            Configuration("00:00:02"),
            Environments.Development,
            configureOptions: null,
            logger);

        // Two resolutions: the options pipeline caches, so the startup line is not repeated.
        _ = provider.GetRequiredService<IOptions<ProcessSupervisorOptions>>().Value;
        _ = provider.GetRequiredService<IOptions<ProcessSupervisorOptions>>().Value;

        var startupLines = logger.Entries
            .Where(entry => entry.Level == LogLevel.Information
                && entry.Message.Contains("status reconcile interval", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.AreEqual(1, startupLines.Count);
        StringAssert.Contains(startupLines[0].Message, "00:00:02");
    }

    [TestMethod]
    public void AFastIntervalOutsideDevelopmentWarns()
    {
        var logger = new CapturingLoggerProvider();

        using var provider = BuildProvider(
            Configuration("00:00:00.500"),
            Environments.Production,
            configureOptions: null,
            logger);

        _ = provider.GetRequiredService<IOptions<ProcessSupervisorOptions>>().Value;

        Assert.AreEqual(
            1,
            logger.Entries.Count(entry => entry.Level == LogLevel.Warning
                && entry.Message.Contains("below one second", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void AFastIntervalInDevelopmentIsNotAWarning()
    {
        var logger = new CapturingLoggerProvider();

        using var provider = BuildProvider(
            Configuration("00:00:00.500"),
            Environments.Development,
            configureOptions: null,
            logger);

        _ = provider.GetRequiredService<IOptions<ProcessSupervisorOptions>>().Value;

        Assert.AreEqual(
            0,
            logger.Entries.Count(entry => entry.Level >= LogLevel.Warning),
            "Development is the environment the fast interval exists for.");
    }

    [TestMethod]
    public void TheDevelopmentSettingsRequestAOneSecondInterval()
    {
        // The acceptance criterion is empirical (a stop applies within about a second), and this
        // key is what makes it true. A later edit that drops or raises it must fail here instead
        // of quietly restoring a 30-second development loop.
        var path = FindRepositoryFile(
            Path.Combine("src", "Core", "DotNetCloud.Core.Server", "appsettings.Development.json"));

        using var stream = File.OpenRead(path);
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();

        var options = configuration
            .GetSection(ProcessSupervisorOptions.SectionName)
            .Get<ProcessSupervisorOptions>();

        Assert.IsNotNull(options);
        Assert.AreEqual(TimeSpan.FromSeconds(1), options.StatusReconcileInterval);
    }

    private static IConfiguration Configuration(string interval)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [StatusReconcileOption] = interval,
            })
            .Build();
    }

    private static ServiceProvider BuildProvider(
        IConfiguration? configuration,
        string environmentName,
        Action<ProcessSupervisorOptions>? configureOptions,
        CapturingLoggerProvider? loggerProvider = null)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            if (loggerProvider is not null)
            {
                builder.AddProvider(loggerProvider);
            }
        });

        if (configuration is not null)
        {
            services.AddSingleton(configuration);
        }

        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));
        services.AddProcessSupervisor(configureOptions);

        return services.BuildServiceProvider();
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DotNetCloud.sln")))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new AssertFailedException(
            $"Could not find the repository root (DotNetCloud.sln) above {AppContext.BaseDirectory}.");
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "DotNetCloud.Core.Server.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    /// <summary>Captures formatted log entries so a test can assert on the startup lines.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

        public IReadOnlyCollection<(LogLevel Level, string Message)> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => this;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            _entries.Enqueue((logLevel, formatter(state, exception)));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public void Dispose()
        {
        }
    }
}
