using System.Net;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Infrastructure;

[TestClass]
public class FirewallHelperTests
{
    [TestMethod]
    public void GetLanIpAddress_ReturnsNullOrValidIp()
    {
        var result = FirewallHelper.GetLanIpAddress();

        if (result is not null)
        {
            Assert.IsTrue(
                IPAddress.TryParse(result, out var parsed),
                $"Expected a valid IP address but got: {result}");
            Assert.IsFalse(IPAddress.IsLoopback(parsed), "Should not return a loopback address");
        }

        // null is acceptable (e.g., no network interfaces on CI)
    }

    [TestMethod]
    public void GetLanIpAddress_DoesNotReturnLoopback()
    {
        var result = FirewallHelper.GetLanIpAddress();

        if (result is not null)
        {
            Assert.AreNotEqual("127.0.0.1", result);
            Assert.AreNotEqual("::1", result);
        }
    }

    [TestMethod]
    public void GetLanIpAddress_DoesNotThrow()
    {
        // Should never throw, even in unusual environments
        string? result = null;
        try
        {
            result = FirewallHelper.GetLanIpAddress();
        }
        catch (Exception ex)
        {
            Assert.Fail($"GetLanIpAddress threw unexpectedly: {ex.Message}");
        }

        // Result can be null or a valid IP — either is fine
        if (result is not null)
        {
            Assert.IsTrue(IPAddress.TryParse(result, out _));
        }
    }
}
