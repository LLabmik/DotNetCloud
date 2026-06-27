using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace DotNetCloud.Client.Core.Auth;

/// <summary>
/// Builds HTTP handlers for OAuth flows.
/// </summary>
public static class OAuthHttpClientHandlerFactory
{
    /// <summary>
    /// Creates a pooled <see cref="SocketsHttpHandler"/> tuned for high-throughput scenarios
    /// (16 connections per server, 10 min pool lifetime, HTTP/2, keepalive). The handler
    /// allows self-signed certs for local/private hosts via <see cref="CreatePermissiveSslOptions"/>.
    /// </summary>
    public static SocketsHttpHandler CreatePooledHandler()
    {
        return new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            MaxConnectionsPerServer = 16,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            EnableMultipleHttp2Connections = true,
            KeepAlivePingDelay = TimeSpan.FromSeconds(30),
            SslOptions = CreatePermissiveSslOptions(),
        };
    }

    /// <summary>
    /// Creates <see cref="SslClientAuthenticationOptions"/> that enforces strict TLS
    /// for public hosts but allows self-signed / invalid certs for local/private hosts.
    /// </summary>
    public static SslClientAuthenticationOptions CreatePermissiveSslOptions()
    {
        var sslOptions = new SslClientAuthenticationOptions();
        sslOptions.RemoteCertificateValidationCallback = (_, _, _, errors) =>
        {
            if (errors == SslPolicyErrors.None)
                return true;

            // SocketsHttpHandler sets TargetHost before the callback fires.
            var host = sslOptions.TargetHost;
            if (string.IsNullOrWhiteSpace(host))
                return false;

            return IsLocalOrPrivateHost(host);
        };
        return sslOptions;
    }

    /// <summary>
    /// Creates a handler that keeps strict TLS by default, but allows invalid certs
    /// for local/self-hosted targets commonly used during LAN deployments.
    /// </summary>
    public static HttpMessageHandler CreateHandler() => CreatePooledHandler();

    private static bool IsLocalOrPrivateHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            return IsLoopbackOrPrivate(ip);
        }

        // Single-label hosts are typically local network names (for example: mint22).
        if (!host.Contains('.'))
        {
            return true;
        }

        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".home", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".test", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var addresses = Dns.GetHostAddresses(host);
            return addresses.Length > 0 && Array.TrueForAll(addresses, IsLoopbackOrPrivate);
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsLoopbackOrPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || IsUniqueLocalIpv6(ip);
        }

        return IsPrivateIpv4(ip);
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

    private static bool IsUniqueLocalIpv6(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return bytes.Length == 16 && (bytes[0] & 0xFE) == 0xFC;
    }
}
