using DotNetCloud.Client.Core.Auth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.Auth;

[TestClass]
public class TokenInfoTests
{
    [TestMethod]
    public void IsExpired_WhenExpiresInFuture_ReturnsFalse()
    {
        var info = new TokenInfo
        {
            AccessToken = "abc",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        };

        Assert.IsFalse(info.IsExpired);
    }

    [TestMethod]
    public void IsExpired_WhenExpired_ReturnsTrue()
    {
        var info = new TokenInfo
        {
            AccessToken = "abc",
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1),
        };

        Assert.IsTrue(info.IsExpired);
    }

    [TestMethod]
    public void CanRefresh_WithRefreshToken_ReturnsTrue()
    {
        var info = new TokenInfo
        {
            AccessToken = "abc",
            RefreshToken = "refresh-xyz",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        };

        Assert.IsTrue(info.CanRefresh);
    }

    [TestMethod]
    public void CanRefresh_WithoutRefreshToken_ReturnsFalse()
    {
        var info = new TokenInfo
        {
            AccessToken = "abc",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        };

        Assert.IsFalse(info.CanRefresh);
    }
}
