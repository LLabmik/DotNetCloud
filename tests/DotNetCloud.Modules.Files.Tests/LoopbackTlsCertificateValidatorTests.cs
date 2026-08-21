using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DotNetCloud.Modules.Files.Data.Security;

namespace DotNetCloud.Modules.Files.Tests;

[TestClass]
public class LoopbackTlsCertificateValidatorTests
{
    [TestMethod]
    public void IsLoopbackHost_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(LoopbackTlsCertificateValidator.IsLoopbackHost(null));
        Assert.IsFalse(LoopbackTlsCertificateValidator.IsLoopbackHost(""));
        Assert.IsFalse(LoopbackTlsCertificateValidator.IsLoopbackHost("   "));
    }

    [TestMethod]
    public void IsLoopbackHost_LocalhostVariants_ReturnsTrue()
    {
        Assert.IsTrue(LoopbackTlsCertificateValidator.IsLoopbackHost("localhost"));
        Assert.IsTrue(LoopbackTlsCertificateValidator.IsLoopbackHost("LOCALHOST"));
        Assert.IsTrue(LoopbackTlsCertificateValidator.IsLoopbackHost("127.0.0.1"));
        Assert.IsTrue(LoopbackTlsCertificateValidator.IsLoopbackHost("::1"));
    }

    [TestMethod]
    public void IsLoopbackHost_NonLoopback_ReturnsFalse()
    {
        Assert.IsFalse(LoopbackTlsCertificateValidator.IsLoopbackHost("cloud.dotnetcloud.net"));
        Assert.IsFalse(LoopbackTlsCertificateValidator.IsLoopbackHost("192.168.0.25"));
    }

    [TestMethod]
    public void Validate_LoopbackWithCertificateErrors_ReturnsTrue()
    {
        using var certificate = CreateSelfSignedCertificate();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:9980/hosting/discovery");

        var result = LoopbackTlsCertificateValidator.Validate(
            request, certificate, null, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Validate_NonLoopbackWithNoErrors_ReturnsTrue()
    {
        using var certificate = CreateSelfSignedCertificate();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://collabora.example.com/hosting/discovery");

        var result = LoopbackTlsCertificateValidator.Validate(
            request, certificate, null, SslPolicyErrors.None);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Validate_NonLoopbackWithCertificateErrors_ReturnsFalse()
    {
        using var certificate = CreateSelfSignedCertificate();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://collabora.example.com/hosting/discovery");

        var result = LoopbackTlsCertificateValidator.Validate(
            request, certificate, null, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Validate_NullCertificate_ReturnsFalse()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:9980/hosting/discovery");

        var result = LoopbackTlsCertificateValidator.Validate(
            request, null, null, SslPolicyErrors.RemoteCertificateNotAvailable);

        Assert.IsFalse(result);
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
    }
}
