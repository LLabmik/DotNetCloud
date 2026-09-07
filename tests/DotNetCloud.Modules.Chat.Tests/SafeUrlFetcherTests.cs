using System.Net;
using DotNetCloud.Modules.Chat.Data.Services;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for the SSRF guard used by chat link previews.
/// </summary>
[TestClass]
public class SafeUrlFetcherTests
{
    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("127.0.0.2")]
    [DataRow("10.0.0.1")]
    [DataRow("10.255.255.255")]
    [DataRow("172.16.0.1")]
    [DataRow("172.31.255.255")]
    [DataRow("192.168.0.1")]
    [DataRow("192.168.255.254")]
    [DataRow("169.254.169.254")] // cloud metadata endpoint
    [DataRow("100.64.0.1")]      // CGNAT
    [DataRow("100.127.255.254")] // CGNAT
    [DataRow("0.0.0.0")]
    public void IsPrivateOrSpecialIp_PrivateAndSpecial_ReturnsTrue(string address)
    {
        Assert.IsTrue(SafeUrlFetcher.IsPrivateOrSpecialIp(IPAddress.Parse(address)), $"{address} should be blocked");
    }

    [TestMethod]
    [DataRow("8.8.8.8")]
    [DataRow("1.1.1.1")]
    [DataRow("151.101.65.140")]
    [DataRow("93.184.216.34")]
    public void IsPrivateOrSpecialIp_PublicIp_ReturnsFalse(string address)
    {
        Assert.IsFalse(SafeUrlFetcher.IsPrivateOrSpecialIp(IPAddress.Parse(address)), $"{address} should be allowed");
    }

    [TestMethod]
    [DataRow("::1")]
    [DataRow("fe80::1")]           // IPv6 link-local
    [DataRow("fec0::1")]           // IPv6 site-local
    [DataRow("::ffff:192.168.1.5")] // IPv4-mapped private
    public void IsPrivateOrSpecialIp_IPv6Private_ReturnsTrue(string address)
    {
        Assert.IsTrue(SafeUrlFetcher.IsPrivateOrSpecialIp(IPAddress.Parse(address)), $"{address} should be blocked");
    }

    [TestMethod]
    [DataRow("2606:4700:4700::1111")]
    public void IsPrivateOrSpecialIp_IPv6Public_ReturnsFalse(string address)
    {
        Assert.IsFalse(SafeUrlFetcher.IsPrivateOrSpecialIp(IPAddress.Parse(address)), $"{address} should be allowed");
    }

    [TestMethod]
    [DataRow("192.168.1.1")]
    [DataRow("10.1.2.3")]
    [DataRow("127.0.0.1")]
    [DataRow("::1")]
    public void IsBlockedIp_LiteralPrivateHost_ReturnsTrue(string host)
    {
        Assert.IsTrue(SafeUrlFetcher.IsBlockedIp(host), $"{host} should be blocked");
    }

    [TestMethod]
    [DataRow("example.com")]
    [DataRow("github.com")]
    [DataRow("8.8.8.8")]
    public void IsBlockedIp_PublicHost_ReturnsFalse(string host)
    {
        Assert.IsFalse(SafeUrlFetcher.IsBlockedIp(host), $"{host} should be allowed");
    }
}
