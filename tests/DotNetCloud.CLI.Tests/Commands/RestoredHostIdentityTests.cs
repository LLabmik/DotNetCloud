using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Commands;

/// <summary>
/// Tests for restoring onto a machine with a different host name. A backup carries the old machine's
/// identity (TLS certificate + Collabora alias group); these tests pin down that a restore onto new
/// hardware re-issues the leaf for the new name while keeping the restored root CA — so devices that
/// already trust it keep working — and repoints Collabora.
/// </summary>
[TestClass]
public class RestoredHostIdentityTests
{
    private readonly List<string> _tempDirectories = [];
    private readonly List<string> _warnings = [];

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var directory in _tempDirectories)
        {
            try
            { Directory.Delete(directory, recursive: true); }
            catch { /* best effort */ }
        }

        _tempDirectories.Clear();
        _warnings.Clear();
    }

    [TestMethod]
    public void CreateServerCertificate_IssuedByRoot_ContainsHostAndLoopback()
    {
        using var root = RestoredHostIdentity.CreateRootCa();
        using var leaf = RestoredHostIdentity.CreateServerCertificate("newbox", root);

        var names = RestoredHostIdentity.GetDnsNames(leaf);

        CollectionAssert.Contains(names.ToList(), "newbox");
        CollectionAssert.Contains(names.ToList(), "localhost");
        Assert.AreEqual("CN=newbox", leaf.Subject);
        Assert.IsTrue(leaf.HasPrivateKey);
        Assert.IsTrue(RestoredHostIdentity.HostCoveredBy(leaf, "NEWBOX"));
        Assert.IsFalse(RestoredHostIdentity.HostCoveredBy(leaf, "oldbox"));
    }

    [TestMethod]
    public void BuildPemBundle_WithPrivateKey_WritesKeyAndBothCertificates()
    {
        using var root = RestoredHostIdentity.CreateRootCa();
        using var leaf = RestoredHostIdentity.CreateServerCertificate("newbox", root);

        var pem = RestoredHostIdentity.BuildPemBundle(leaf, root, includePrivateKey: true);

        StringAssert.Contains(pem, "BEGIN RSA PRIVATE KEY");
        Assert.AreEqual(2, CountOccurrences(pem, "BEGIN CERTIFICATE"));
    }

    [TestMethod]
    public void Repoint_DifferentHost_ReissuesLeafAndKeepsRootCa()
    {
        var directory = CreateTempDirectory();
        var certPath = Path.Combine(directory, "certs", "dotnetcloud-selfsigned.pfx");
        Directory.CreateDirectory(Path.GetDirectoryName(certPath)!);

        using var root = RestoredHostIdentity.CreateRootCa();
        using (var oldLeaf = RestoredHostIdentity.CreateServerCertificate("oldbox", root))
        {
            File.WriteAllBytes(certPath, new X509Certificate2Collection { oldLeaf, root }
                .Export(X509ContentType.Pfx, string.Empty)!);
        }

        var collaboraDirectory = Path.Combine(directory, "coolwsd");
        Directory.CreateDirectory(collaboraDirectory);
        File.WriteAllText(Path.Combine(collaboraDirectory, "coolwsd.xml"), CoolwsdXml("oldbox"));
        File.WriteAllText(Path.Combine(collaboraDirectory, "cert.pem"), "OLD-CERT");

        var config = CreateConfig(certPath, "oldbox");
        CliConfig? persisted = null;

        var result = RestoredHostIdentity.Repoint(
            config,
            "tester",
            _warnings.Add,
            new HostRepointOptions(
                Persist: c => persisted = c,
                CollaboraDirectory: collaboraDirectory,
                RestartCollabora: false));

        var currentHost = RestoredHostIdentity.GetCurrentHost();

        Assert.IsNotNull(result);
        Assert.AreEqual("oldbox", result.OldHost);
        Assert.AreEqual(currentHost, result.NewHost);
        Assert.IsTrue(result.CertificateReissued);
        Assert.IsFalse(result.RootCaChanged, "the restored root CA signs the new leaf, so client trust survives");
        Assert.IsTrue(result.CollaboraUpdated);

        // The new leaf covers this machine and is still issued by the original root CA.
        var reissued = X509CertificateLoader.LoadPkcs12CollectionFromFile(certPath, string.Empty);
        var newLeaf = reissued.Cast<X509Certificate2>()
            .First(c => !c.Subject.Contains("Root CA", StringComparison.Ordinal));
        var restoredRoot = reissued.Cast<X509Certificate2>()
            .Single(c => c.Subject.Contains("Root CA", StringComparison.Ordinal));

        Assert.IsTrue(RestoredHostIdentity.HostCoveredBy(newLeaf, currentHost));
        Assert.AreEqual(root.Thumbprint, restoredRoot.Thumbprint, "the root CA must survive re-issue");
        Assert.IsTrue(newLeaf.HasPrivateKey);

        // Companion files follow the new leaf.
        var pemPath = Path.ChangeExtension(certPath, ".pem");
        StringAssert.Contains(File.ReadAllText(pemPath), "BEGIN RSA PRIVATE KEY");
        Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(certPath)!, "dotnetcloud-root-ca.crt")));

        // Config and Collabora now name this machine.
        Assert.IsNotNull(persisted);
        Assert.AreEqual(currentHost, persisted.SelfSignedTlsHost);

        var coolwsd = File.ReadAllText(Path.Combine(collaboraDirectory, "coolwsd.xml"));
        StringAssert.Contains(coolwsd, $"https://{currentHost}:5443");
        Assert.IsFalse(coolwsd.Contains("oldbox", StringComparison.Ordinal), "the managed alias block must be repointed");
        StringAssert.Contains(File.ReadAllText(Path.Combine(collaboraDirectory, "cert.pem")), "BEGIN CERTIFICATE");
    }

    [TestMethod]
    public void Repoint_SameHost_ReturnsNullAndOnlyFreshensConfig()
    {
        var directory = CreateTempDirectory();
        var certPath = Path.Combine(directory, "dotnetcloud-selfsigned.pfx");
        var currentHost = RestoredHostIdentity.GetCurrentHost();

        using var root = RestoredHostIdentity.CreateRootCa();
        using (var leaf = RestoredHostIdentity.CreateServerCertificate(currentHost, root))
        {
            File.WriteAllBytes(certPath, new X509Certificate2Collection { leaf, root }
                .Export(X509ContentType.Pfx, string.Empty)!);
        }

        var config = CreateConfig(certPath, "stale-name");
        CliConfig? persisted = null;

        var result = RestoredHostIdentity.Repoint(
            config, "tester", _warnings.Add,
            new HostRepointOptions(Persist: c => persisted = c, RestartCollabora: false));

        Assert.IsNull(result);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(currentHost, persisted.SelfSignedTlsHost);
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void Repoint_CertificateWithoutRootPrivateKey_GeneratesNewRootCa()
    {
        var directory = CreateTempDirectory();
        var certPath = Path.Combine(directory, "dotnetcloud-selfsigned.pfx");

        using var root = RestoredHostIdentity.CreateRootCa();
        using var leaf = RestoredHostIdentity.CreateServerCertificate("oldbox", root);
        File.WriteAllBytes(certPath, leaf.Export(X509ContentType.Pfx, string.Empty)!);

        var config = CreateConfig(certPath, "oldbox");

        var result = RestoredHostIdentity.Repoint(
            config, "tester", _warnings.Add,
            new HostRepointOptions(Persist: _ => { }, RestartCollabora: false));

        Assert.IsNotNull(result);
        Assert.IsTrue(result.RootCaChanged);
        Assert.IsTrue(_warnings.Any(w => w.Contains("root CA", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Repoint_HttpsDisabled_DoesNothing()
    {
        var directory = CreateTempDirectory();
        var config = CreateConfig(Path.Combine(directory, "missing.pfx"), "oldbox");
        config.EnableHttps = false;

        var result = RestoredHostIdentity.Repoint(
            config, "tester", _warnings.Add,
            new HostRepointOptions(Persist: _ => { }, RestartCollabora: false));

        Assert.IsNull(result);
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void Repoint_MissingCertificate_WarnsInsteadOfThrowing()
    {
        var directory = CreateTempDirectory();
        var config = CreateConfig(Path.Combine(directory, "missing.pfx"), "oldbox");

        var result = RestoredHostIdentity.Repoint(
            config, "tester", _warnings.Add,
            new HostRepointOptions(Persist: _ => { }, RestartCollabora: false));

        Assert.IsNull(result);
        Assert.HasCount(1, _warnings);
        StringAssert.Contains(_warnings[0], "setup");
    }

    [TestMethod]
    public void UpdateCollaboraAliasGroup_ManagedBlock_RepointsOnlyInsideBlock()
    {
        const string xml = """
            <config>
              <!-- https://oldbox:5443 mentioned in an unrelated comment stays -->
              <!-- dotnetcloud-managed-start -->
              <group>
                <host allow="true">https://oldbox:5443</host>
                <alias>https://oldbox</alias>
                <alias>https://localhost:5443</alias>
              </group>
              <!-- dotnetcloud-managed-end -->
              <other>https://oldbox:5443</other>
            </config>
            """;

        var (content, changed) = RestoredHostIdentity.UpdateCollaboraAliasGroup(xml, "oldbox", "newbox");

        Assert.IsTrue(changed);
        StringAssert.Contains(content, "https://newbox:5443");
        StringAssert.Contains(content, "<alias>https://newbox</alias>");
        StringAssert.Contains(content, "unrelated comment stays");   // outside the block, untouched
        StringAssert.Contains(content, "<other>https://oldbox:5443</other>");
        Assert.AreEqual(2, CountOccurrences(content, "oldbox"),
            "the unrelated comment and the element outside the managed block keep the old host");
    }

    [TestMethod]
    public void UpdateCollaboraAliasGroup_NoManagedBlock_ReturnsUnchanged()
    {
        const string xml = "<config><group><host>https://oldbox:5443</host></group></config>";

        var (content, changed) = RestoredHostIdentity.UpdateCollaboraAliasGroup(xml, "oldbox", "newbox");

        Assert.IsFalse(changed);
        Assert.AreEqual(xml, content);
    }

    private static string CoolwsdXml(string host) => $"""
        <config>
        <!-- dotnetcloud-managed-start -->
        <group>
            <host desc="DotNetCloud single-origin public host" allow="true">https://{host}:5443</host>
            <alias>https://{host}:5443</alias>
            <alias>https://localhost:5443</alias>
        </group>
        <!-- dotnetcloud-managed-end -->
        </config>
        """;

    private static CliConfig CreateConfig(string certificatePath, string selfSignedHost) => new()
    {
        EnableHttps = true,
        UseSelfSignedTls = true,
        TlsCertificatePath = certificatePath,
        SelfSignedTlsHost = selfSignedHost,
    };

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dnc-host-identity-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        return directory;
    }
}
