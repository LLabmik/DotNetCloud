using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

[TestClass]
public class ModuleDiscoveryServiceTests
{
    private string _tempDir = null!;
    private ModuleDiscoveryService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetcloud-tests", Guid.CreateVersion7().ToString());
        Directory.CreateDirectory(_tempDir);

        var options = Options.Create(new ProcessSupervisorOptions
        {
            ModulesDirectory = _tempDir
        });

        _service = new ModuleDiscoveryService(
            NullLogger<ModuleDiscoveryService>.Instance,
            options);
    }

    [TestMethod]
    public void DiscoverModulesWhenEmptyDirectoryThenReturnsEmpty()
    {
        var modules = _service.DiscoverModules();

        Assert.AreEqual(0, modules.Count);
    }

    [TestMethod]
    public void DiscoverModulesWhenDirectoryDoesNotExistThenCreatesItAndReturnsEmpty()
    {
        var nonExistentDir = Path.Combine(_tempDir, "nonexistent");
        var options = Options.Create(new ProcessSupervisorOptions
        {
            ModulesDirectory = nonExistentDir
        });
        var service = new ModuleDiscoveryService(
            NullLogger<ModuleDiscoveryService>.Instance,
            options);

        var modules = service.DiscoverModules();

        Assert.AreEqual(0, modules.Count);
        Assert.IsTrue(Directory.Exists(nonExistentDir));
    }

    [TestMethod]
    public void DiscoverModulesWhenModuleWithDllThenDiscoversIt()
    {
        CreateModuleOnDisk("test.module", hasDll: true);

        var modules = _service.DiscoverModules();

        Assert.AreEqual(1, modules.Count);
        Assert.AreEqual("test.module", modules[0].ModuleId);
        Assert.IsTrue(modules[0].ExecutablePath.EndsWith("test.module.dll"));
    }

    [TestMethod]
    public void DiscoverModulesWhenModuleWithExeThenDiscoversIt()
    {
        CreateModuleOnDisk("test.module", hasDll: false, hasExe: true);

        var modules = _service.DiscoverModules();

        Assert.AreEqual(1, modules.Count);
        Assert.IsTrue(modules[0].ExecutablePath.EndsWith("test.module.exe"));
    }

    [TestMethod]
    public void DiscoverModulesWhenModuleHasNoExecutableThenSkipsIt()
    {
        var moduleDir = Path.Combine(_tempDir, "broken.module");
        Directory.CreateDirectory(moduleDir);
        // No .dll or .exe file

        var modules = _service.DiscoverModules();

        Assert.AreEqual(0, modules.Count);
    }

    [TestMethod]
    public void DiscoverModulesWhenModuleHasManifestThenManifestPathIsSet()
    {
        CreateModuleOnDisk("test.module", hasDll: true, hasManifest: true);

        var modules = _service.DiscoverModules();

        Assert.AreEqual(1, modules.Count);
        Assert.IsNotNull(modules[0].ManifestPath);
        Assert.IsTrue(modules[0].ManifestPath!.EndsWith("manifest.json"));
    }

    [TestMethod]
    public void DiscoverModulesWhenModuleHasNoManifestThenManifestPathIsNull()
    {
        CreateModuleOnDisk("test.module", hasDll: true, hasManifest: false);

        var modules = _service.DiscoverModules();

        Assert.AreEqual(1, modules.Count);
        Assert.IsNull(modules[0].ManifestPath);
    }

    [TestMethod]
    public void DiscoverModulesWhenMultipleModulesThenDiscoversAll()
    {
        CreateModuleOnDisk("module.alpha", hasDll: true);
        CreateModuleOnDisk("module.beta", hasDll: true);
        CreateModuleOnDisk("module.gamma", hasDll: true);

        var modules = _service.DiscoverModules();

        Assert.AreEqual(3, modules.Count);
        var ids = modules.Select(m => m.ModuleId).OrderBy(x => x).ToList();
        CollectionAssert.AreEqual(
            new[] { "module.alpha", "module.beta", "module.gamma" },
            ids);
    }

    [TestMethod]
    public void DiscoverModuleThenReturnsMatchingModule()
    {
        CreateModuleOnDisk("target.module", hasDll: true, hasManifest: true);

        var module = _service.DiscoverModule("target.module");

        Assert.IsNotNull(module);
        Assert.AreEqual("target.module", module.ModuleId);
    }

    [TestMethod]
    public void DiscoverModuleWhenNotFoundThenReturnsNull()
    {
        var module = _service.DiscoverModule("nonexistent.module");

        Assert.IsNull(module);
    }

    [TestMethod]
    public void DiscoverModuleWhenNullIdThenThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => _service.DiscoverModule(null!));
    }

    [TestMethod]
    public void DiscoverModulesWhenModuleDirectorySetThenSetsCorrectly()
    {
        CreateModuleOnDisk("test.module", hasDll: true);

        var modules = _service.DiscoverModules();

        Assert.AreEqual(1, modules.Count);
        Assert.AreEqual(_tempDir, Path.GetDirectoryName(modules[0].ModuleDirectory));
    }

    [TestMethod]
    public void DiscoverModule_ModuleIdWithNewlines_WritesSingleSanitizedLogEntry()
    {
        var logger = new CapturingLogger<ModuleDiscoveryService>();
        var service = new ModuleDiscoveryService(
            logger,
            Options.Create(new ProcessSupervisorOptions { ModulesDirectory = _tempDir }));

        var module = service.DiscoverModule("missing.module\r\nFAKE LOG ENTRY");

        Assert.IsNull(module);
        var message = logger.Messages.Single(m => m.Contains("Module directory not found"));
        Assert.IsFalse(message.Contains('\r'), "CR must not reach the log sink");
        Assert.IsFalse(message.Contains('\n'), "LF must not reach the log sink");
        Assert.IsTrue(message.Contains("FAKE LOG ENTRY"), "the value should remain visible, only neutralized");
    }

    [TestMethod]
    public void RemoveModuleDirectory_ModuleIdEscapesModulesRoot_ReturnsFalseAndKeepsOutsideDirectory()
    {
        var outsideDir = Path.Combine(Path.GetDirectoryName(_tempDir)!, $"{Guid.CreateVersion7():N}-outside");
        Directory.CreateDirectory(outsideDir);

        try
        {
            var removed = _service.RemoveModuleDirectory($"../{Path.GetFileName(outsideDir)}");

            Assert.IsFalse(removed);
            Assert.IsTrue(Directory.Exists(outsideDir), "a traversal id must never delete outside the modules root");
        }
        finally
        {
            try
            { Directory.Delete(outsideDir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private void CreateModuleOnDisk(
        string moduleId,
        bool hasDll = false,
        bool hasExe = false,
        bool hasManifest = false)
    {
        var moduleDir = Path.Combine(_tempDir, moduleId);
        Directory.CreateDirectory(moduleDir);

        if (hasDll)
        {
            File.WriteAllText(Path.Combine(moduleDir, $"{moduleId}.dll"), "fake-dll");
        }

        if (hasExe)
        {
            File.WriteAllText(Path.Combine(moduleDir, $"{moduleId}.exe"), "fake-exe");
        }

        if (hasManifest)
        {
            File.WriteAllText(Path.Combine(moduleDir, "manifest.json"), $$"""
                {
                    "id": "{{moduleId}}",
                    "name": "{{moduleId}}",
                    "version": "1.0.0"
                }
                """);
        }
    }

    /// <summary>Logger that records the formatted message of every log entry.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
