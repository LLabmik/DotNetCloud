using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Email.Data.Services;
using DotNetCloud.Modules.Email.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq;

namespace DotNetCloud.Modules.Email.Tests;

[TestClass]
public class EmailRuleServiceTests
{
    private EmailDbContext _db;
    private EmailRuleService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;
    private static readonly Guid UserId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EmailDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new EmailDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _service = new EmailRuleService(_db, Enumerable.Empty<IEmailProvider>(), _eventBusMock.Object, NullLogger<EmailRuleService>.Instance);
        _caller = new CallerContext(UserId, new[] { "user" }, CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task ListAsync_NoRules_ReturnsEmptyList()
    {
        var results = await _service.ListAsync(_caller, null);
        Assert.IsNotNull(results);
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task GetAsync_NonExistentRule_ReturnsNull()
    {
        var result = await _service.GetAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }
}
