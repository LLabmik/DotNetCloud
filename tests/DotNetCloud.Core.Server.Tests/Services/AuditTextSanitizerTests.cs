using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Audit;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the module-supplied audit text hardening (finding F14): newlines and control
/// characters are stripped (no log forging), and every field is length-capped.
/// </summary>
[TestClass]
public class AuditTextSanitizerTests
{
    private string _dbName = null!;

    [TestInitialize]
    public void Setup() => _dbName = $"AuditTextSanitizerTests_{Guid.NewGuid():N}";

    private CoreDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options,
            new PostgreSqlNamingStrategy());

    [TestMethod]
    public void Clean_StripsNewlinesAndControlCharacters()
    {
        var result = AuditTextSanitizer.Clean("line1\r\nline2\u0000\u0007line3", 100);

        Assert.AreEqual("line1line2line3", result);
    }

    [TestMethod]
    public void Clean_TruncatesToTheMaximumLength()
    {
        var result = AuditTextSanitizer.Clean(new string('a', 200), 50);

        Assert.AreEqual(50, result!.Length);
    }

    [TestMethod]
    public void Clean_TrimsSurroundingWhitespace()
    {
        Assert.AreEqual("value", AuditTextSanitizer.Clean("   value\t", 100));
    }

    [TestMethod]
    public void Clean_NullInput_ReturnsDefaultWhenGiven()
    {
        Assert.AreEqual("unknown", AuditTextSanitizer.Clean(null, 100, "unknown"));
        Assert.IsNull(AuditTextSanitizer.Clean(null, 100));
    }

    [TestMethod]
    public void Clean_AllControlCharacters_FallsBackToDefault()
    {
        Assert.AreEqual("fallback", AuditTextSanitizer.Clean("\r\n\t", 100, "fallback"));
    }

    [TestMethod]
    public void CleanRequired_AlwaysReturnsNonNull()
    {
        Assert.AreEqual("d", AuditTextSanitizer.CleanRequired(null, 100, "d"));
        Assert.AreEqual("d", AuditTextSanitizer.CleanRequired("", 100, "d"));
    }

    [TestMethod]
    public async Task LogAsync_SanitizesAndCapsEveryModuleSuppliedField()
    {
        await using var db = CreateContext();
        var service = new AuditLogService(db, NullLogger<AuditLogService>.Instance);

        await service.LogAsync(new AuditEntry
        {
            Caller = CallerContext.CreateSystemContext(),
            ModuleId = "vendor.receipts\r\nfake-entry",
            Action = AuditAction.ModuleEventRejected,
            EntityType = new string('E', 500),
            EntityId = Guid.CreateVersion7(),
            Description = "first line\r\ninjected second line",
        });

        var log = await db.AuditLogs.AsNoTracking().SingleAsync();

        Assert.IsFalse(log.ModuleId.Contains('\n'), "newlines must never reach the audit table");
        Assert.IsFalse(log.Description!.Contains('\n'), "newlines must never reach the audit table");
        Assert.AreEqual(AuditTextSanitizer.MaxEntityTypeLength, log.EntityType.Length);
        Assert.AreEqual("vendor.receiptsfake-entry", log.ModuleId);
        Assert.AreEqual("first lineinjected second line", log.Description);
    }
}
