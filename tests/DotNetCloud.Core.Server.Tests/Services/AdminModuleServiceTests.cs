using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Regression tests for module start/stop status persistence. Mirrors production
/// by configuring <see cref="QueryTrackingBehavior.NoTracking"/> so the tests fail
/// if a status update forgets to use <c>AsTracking()</c>.
/// </summary>
[TestClass]
public class AdminModuleServiceTests
{
    private const string ModuleId = "dotnetcloud.testmodule";

    // Unique per test method: the InMemory store is keyed by name, so a fresh
    // name avoids PK collisions across tests while still being reused between
    // the seed and verify contexts WITHIN a single test.
    private string _dbName = null!;

    private CoreDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(_dbName)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .Options,
            new PostgreSqlNamingStrategy());

    [TestInitialize]
    public void Setup()
    {
        _dbName = $"AdminModuleServiceTests_{Guid.NewGuid():N}";
    }

    private static Mock<IProcessSupervisor> CreateSupervisor()
    {
        var supervisor = new Mock<IProcessSupervisor>();
        supervisor
            .Setup(s => s.StartModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        supervisor
            .Setup(s => s.StopModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return supervisor;
    }

    private static AdminModuleService CreateService(CoreDbContext db, IProcessSupervisor supervisor) =>
        new(db, supervisor, NullLogger<AdminModuleService>.Instance);

    private static void SeedModule(CoreDbContext db, string status, bool isRequired = false)
    {
        db.InstalledModules.Add(new InstalledModule
        {
            ModuleId = ModuleId,
            Version = "1.0.0",
            Status = status,
            InstalledAt = DateTime.UtcNow,
            IsRequired = isRequired
        });
        db.SaveChanges();
    }

    [TestMethod]
    public async Task StartModuleAsync_PersistsEnabledStatus()
    {
        using (var seed = CreateContext())
        {
            SeedModule(seed, "Disabled");
            var service = CreateService(seed, CreateSupervisor().Object);
            var result = await service.StartModuleAsync(ModuleId, CancellationToken.None);
            Assert.IsTrue(result);
        }

        using var verify = CreateContext();
        var module = await verify.InstalledModules.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == ModuleId);
        Assert.IsNotNull(module);
        Assert.AreEqual("Enabled", module!.Status);
    }

    [TestMethod]
    public async Task StopModuleAsync_PersistsDisabledStatus()
    {
        using (var seed = CreateContext())
        {
            SeedModule(seed, "Enabled");
            var service = CreateService(seed, CreateSupervisor().Object);
            var result = await service.StopModuleAsync(ModuleId, CancellationToken.None);
            Assert.IsTrue(result);
        }

        using var verify = CreateContext();
        var module = await verify.InstalledModules.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == ModuleId);
        Assert.IsNotNull(module);
        Assert.AreEqual("Disabled", module!.Status);
    }
}
