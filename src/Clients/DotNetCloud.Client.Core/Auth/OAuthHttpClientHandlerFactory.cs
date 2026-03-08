using System.Net;
using System.Net.Http;
using System.Net.Security;

namespace DotNetCloud.Client.Core.Auth;

/// <summary>
/// Builds HTTP handlers for OAuth flows.
/// </summary>
public static class OAuthHttpClientHandlerFactory
{
    /// <summary>
    /// Creates a handler that keeps strict TLS by default, but allows invalid certs
    /// for local/self-hosted targets commonly used during LAN deployments.
    /// </summary>
    public static HttpMessageHandler CreateHandler()
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = static (request, _, _, errors) =>
        {
            if (errors == SslPolicyErrors.None)
            {
                return true;
            }

            var host = request?.RequestUri?.Host;
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            // Keep strict TLS for public internet hosts.
            return IsLocalOrPrivateHost(host);
        };

        return handler;
    }

    private static bool IsLocalOrPrivateHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            return IPAddress.IsLoopback(ip) || IsPrivateIpv4(ip);
        }

        // Single-label hosts are typically local network names (for example: mint22).
        if (!host.Contains('.'))
        {
            return true;
        }

        return false;
    }

    private static bool IsPrivateIpv4(IPAddress ip)
    {
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
