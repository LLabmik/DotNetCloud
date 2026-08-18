using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Shared TLS validation callback for loopback HTTP clients.
/// </summary>
internal static class LoopbackCertificateValidator
{
    /// <summary>
    /// Accepts TLS errors only when the sole issue is a hostname mismatch
    /// (e.g. connecting to localhost with a cert for cloud.dotnetcloud.net).
    /// </summary>
    public static bool Validate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        return sslPolicyErrors == SslPolicyErrors.None
            || sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch;
    }
}
