using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Email.Data.Services;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Email.Tests;

[TestClass]
public class EmailAccountServiceTests
{
    private EmailDbContext _db;
    private EmailAccountService _service;
    private CallerContext _caller;
    private static readonly Guid UserId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EmailDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new EmailDbContext(options);
        var protectionProvider = DataProtectionProvider.Create("DotNetCloud.Test");
        var encryption = new EmailCredentialEncryptionService(protectionProvider);
        _service = new EmailAccountService(_db, encryption, NullLogger<EmailAccountService>.Instance);
        _caller = new CallerContext(UserId, new[] { "user" }, CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task CreateAsync_ValidRequest_ReturnsAccount()
    {
        var request = new CreateEmailAccountRequest
        {
            ProviderType = EmailProviderType.ImapSmtp,
            EmailAddress = "test@example.com",
            DisplayName = "Test User",
            CredentialsJson = "{\"imap_server\":\"imap.example.com\"}"
        };

        var result = await _service.CreateAsync(request, _caller);

        Assert.IsNotNull(result);
        Assert.AreNotEqual(Guid.Empty, result.Id);
        Assert.AreEqual("test@example.com", result.EmailAddress);
    }

    [TestMethod]
    public async Task ListAsync_NoAccounts_ReturnsEmptyList()
    {
        var results = await _service.ListAsync(_caller);

        Assert.IsNotNull(results);
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task ListAsync_MultipleAccounts_ReturnsAllOwnedByCaller()
    {
        await _service.CreateAsync(new CreateEmailAccountRequest
        {
            ProviderType = EmailProviderType.ImapSmtp,
            EmailAddress = "a@example.com",
            DisplayName = "A",
            CredentialsJson = "{}"
        }, _caller);
        await _service.CreateAsync(new CreateEmailAccountRequest
        {
            ProviderType = EmailProviderType.ImapSmtp,
            EmailAddress = "b@example.com",
            DisplayName = "B",
            CredentialsJson = "{}"
        }, _caller);

        var results = await _service.ListAsync(_caller);

        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public async Task GetAsync_ExistingAccount_ReturnsAccount()
    {
        var created = await _service.CreateAsync(new CreateEmailAccountRequest
        {
            ProviderType = EmailProviderType.ImapSmtp,
            EmailAddress = "gettest@example.com",
            DisplayName = "Get Test",
            CredentialsJson = "{}"
        }, _caller);

        var result = await _service.GetAsync(created.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(created.Id, result.Id);
    }

    [TestMethod]
    public async Task GetAsync_NonExistentAccount_ReturnsNull()
    {
        var result = await _service.GetAsync(Guid.NewGuid(), _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAsync_ExistingAccount_RemovesAccount()
    {
        var created = await _service.CreateAsync(new CreateEmailAccountRequest
        {
            ProviderType = EmailProviderType.ImapSmtp,
            EmailAddress = "deletetest@example.com",
            DisplayName = "Delete Test",
            CredentialsJson = "{}"
        }, _caller);

        await _service.DeleteAsync(created.Id, _caller);

        var result = await _service.GetAsync(created.Id, _caller);
        Assert.IsNull(result);
    }
}
