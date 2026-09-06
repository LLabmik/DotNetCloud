using DotNetCloud.Core.Data.Services;
using DotNetCloud.Core.Modules;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Data.Tests.Services;

/// <summary>
/// Tests for <see cref="ModuleSchemaService"/> per-module schema-operation serialization.
/// On first boot two startup paths (ProcessSupervisor pre-spawn schema pass and
/// ModuleUiRegistrationHostedService lazy seed) can call EnsureModuleSchemaAsync for the
/// same module concurrently. Without serialization, two EF MigrateAsync runs for the same
/// module collided with 42P07 ("relation ... already exists") and left the schema
/// half-created — so concurrent calls for the SAME module must be serialized, while calls
/// for DIFFERENT modules must still proceed in parallel.
/// </summary>
[TestClass]
public sealed class ModuleSchemaServiceTests
{
    [TestMethod]
    public async Task EnsureModuleSchemaAsync_ConcurrentSameModule_SerializesProviderCalls()
    {
        var moduleId = "dotnetcloud.tracks";
        var provider = new FakeSchemaProvider(moduleId);
        var service = new ModuleSchemaService(new[] { provider }, NullLogger<ModuleSchemaService>.Instance);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => service.EnsureModuleSchemaAsync(moduleId))
            .ToArray();
        await Task.WhenAll(tasks);

        Assert.AreEqual(8, provider.CallCount);
        Assert.AreEqual(1, provider.PeakConcurrency,
            "Concurrent schema creation for the same module must be serialized to avoid " +
            "EF migration collisions (42P07) on first boot.");
    }

    [TestMethod]
    public async Task EnsureModuleSchemaAsync_ConcurrentDifferentModules_RunsInParallel()
    {
        var provider = new FakeSchemaProvider("dotnetcloud.alpha", "dotnetcloud.beta");
        var service = new ModuleSchemaService(new[] { provider }, NullLogger<ModuleSchemaService>.Instance);

        var tasks = new[]
        {
            service.EnsureModuleSchemaAsync("dotnetcloud.alpha"),
            service.EnsureModuleSchemaAsync("dotnetcloud.beta"),
            service.EnsureModuleSchemaAsync("dotnetcloud.alpha"),
            service.EnsureModuleSchemaAsync("dotnetcloud.beta"),
        };
        await Task.WhenAll(tasks);

        Assert.AreEqual(4, provider.CallCount);
        Assert.IsTrue(provider.PeakConcurrency > 1,
            "Schema creation for different modules should not be blocked behind each other.");
    }

    /// <summary>
    /// Records concurrent <see cref="EnsureSchemaAsync"/> invocations so tests can assert
    /// serialization. Core-managed for the configured module ids.
    /// </summary>
    private sealed class FakeSchemaProvider : IModuleSchemaProvider
    {
        private readonly HashSet<string> _managedModules;
        private readonly object _gate = new();
        private int _active;

        public FakeSchemaProvider(params string[] managedModules)
        {
            _managedModules = new HashSet<string>(managedModules, StringComparer.OrdinalIgnoreCase);
        }

        public int CallCount => _callCount;

        public int PeakConcurrency => _peakConcurrency;

        private int _callCount;
        private int _peakConcurrency;

        public bool IsCoreManaged(string moduleId) => _managedModules.Contains(moduleId);

        public async Task EnsureSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref _active);
            lock (_gate)
            {
                if (active > _peakConcurrency)
                    _peakConcurrency = active;
            }

            Interlocked.Increment(ref _callCount);

            // Simulate a migration pass so overlapping calls would overlap if not serialized.
            await Task.Delay(30, cancellationToken);

            Interlocked.Decrement(ref _active);
        }

        public Task DropSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
