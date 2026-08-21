using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace DotNetCloud.Modules.Files.Data.Security;

/// <summary>
/// Relaxes TLS certificate validation only for loopback hosts (the locally
/// hosted Collabora/coolwsd instance). Non-loopback hosts keep strict
/// certificate validation.
/// </summary>
internal static class LoopbackTlsCertificateValidator
{
    /// <summary>
    /// Validates a TLS certificate for the Collabora HTTP client. Loopback hosts
    /// (<c>localhost</c>, <c>127.0.0.1</c>, <c>::1</c>) accept any presented
    /// certificate — required for the self-signed or name-mismatched certificate
    /// served by the built-in coolwsd. All other hosts require a fully valid
    /// certificate.
    /// </summary>
    public static bool Validate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        // Never accept a missing certificate (would allow a plaintext downgrade).
        if (certificate is null)
        {
            return false;
        }

        return IsLoopbackHost(request.RequestUri?.Host)
            || sslPolicyErrors == SslPolicyErrors.None;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="host"/> is a loopback hostname or
    /// address.
    /// </summary>
    internal static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }
}
