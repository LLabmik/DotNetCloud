using DotNetCloud.Client.SyncService.ContextManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.SyncService.Tests;

/// <summary>Tests for <see cref="SyncContextRegistration"/> defaults and invariants.</summary>
[TestClass]
public class SyncContextRegistrationTests
{
    [TestMethod]
    public void SyncContextRegistration_DefaultFullScanInterval_IsFiveMinutes()
    {
        var reg = new SyncContextRegistration
        {
            Id = Guid.NewGuid(),
            ServerBaseUrl = "https://cloud.example.com",
            UserId = Guid.NewGuid(),
            LocalFolderPath = "/tmp/sync",
            DisplayName = "Test",
            AccountKey = "key",
            OsUserName = "user",
            DataDirectory = "/tmp/data",
        };

        Assert.AreEqual(TimeSpan.FromMinutes(5), reg.FullScanInterval);
    }

    [TestMethod]
    public void SyncContextRegistration_RegisteredAt_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;
        var reg = new SyncContextRegistration
        {
            Id = Guid.NewGuid(),
            ServerBaseUrl = "https://cloud.example.com",
            UserId = Guid.NewGuid(),
            LocalFolderPath = "/tmp/sync",
            DisplayName = "Test",
            AccountKey = "key",
            OsUserName = "user",
            DataDirectory = "/tmp/data",
        };
        var after = DateTime.UtcNow;

        Assert.IsTrue(reg.RegisteredAt >= before && reg.RegisteredAt <= after,
            "RegisteredAt should default to approximately UtcNow.");
    }

    [TestMethod]
    public void AddAccountRequest_DefaultOsUserName_IsCurrentUser()
    {
        var request = new AddAccountRequest
        {
            ServerBaseUrl = "https://cloud.example.com",
            UserId = Guid.NewGuid(),
            LocalFolderPath = "/tmp/sync",
            DisplayName = "Test",
            AccessToken = "tok",
        };

        Assert.AreEqual(Environment.UserName, request.OsUserName);
    }

    [TestMethod]
    public void AddAccountRequest_DefaultFullScanInterval_IsFiveMinutes()
    {
        var request = new AddAccountRequest
        {
            ServerBaseUrl = "https://cloud.example.com",
            UserId = Guid.NewGuid(),
            LocalFolderPath = "/tmp/sync",
            DisplayName = "Test",
            AccessToken = "tok",
        };

        Assert.AreEqual(TimeSpan.FromMinutes(5), request.FullScanInterval);
    }
}
