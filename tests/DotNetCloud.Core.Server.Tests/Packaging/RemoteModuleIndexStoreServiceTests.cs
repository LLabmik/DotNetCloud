using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using DotNetCloud.Core.Modules.Store;
using DotNetCloud.ModulePackaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Tests.Packaging;

/// <summary>
/// Tests for the remote store client: a signed index fetched over HTTPS, cached, replayed-protected,
/// and used to download and verify packages.
/// </summary>
[TestClass]
public class RemoteModuleIndexStoreServiceTests
{
    private const string ModuleId = "org.example.receipts";
    private const string IndexUrl = "https://store.test/index/v1/index.json";
    private const string PackageUrl =
        "packages/org.example.receipts/1.0.0/org.example.receipts-1.0.0.dncpkg";

    private RemoteIndexFixture _fixture = null!;

    [TestInitialize]
    public void Setup() => _fixture = new RemoteIndexFixture();

    [TestCleanup]
    public void Cleanup() => _fixture.Dispose();

    private RemoteModuleIndexStoreService CreateService(
        StubHttpHandler handler, ModuleStoreOptions? options = null) =>
        new(
            new HttpClient(handler),
            Options.Create(options ?? _fixture.CreateOptions()),
            _fixture.Time,
            NullLogger<RemoteModuleIndexStoreService>.Instance);

    [TestMethod]
    public async Task ListPackagesAsync_SignedIndex_MapsEveryVersion()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler);

        var packages = await CreateService(handler).ListPackagesAsync();

        Assert.AreEqual(1, packages.Count);
        var listed = packages[0];
        Assert.AreEqual(ModuleId, listed.ModuleId);
        Assert.AreEqual("1.0.0", listed.Version);
        Assert.AreEqual(RemoteIndexFixture.PackageBytes.Length, listed.SizeBytes);
        Assert.AreEqual(RemoteIndexFixture.PackageSha256, listed.Sha256);
        Assert.AreEqual("example-org", listed.PublisherId);
        Assert.AreEqual(_fixture.PublisherKey.KeyId, listed.KeyId);
        CollectionAssert.Contains(listed.Capabilities.ToArray(), "IStorageProvider");
        Assert.IsFalse(listed.Yanked);
    }

    [TestMethod]
    public async Task ListPackagesAsync_UntrustedIndexKey_Throws()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler, indexKey: Ed25519Signature.GenerateKeyPair("not-pinned"));

        var error = await Assert.ThrowsExactlyAsync<ModulePackageException>(
            () => CreateService(handler).ListPackagesAsync());

        StringAssert.Contains(error.Message, "does not trust");
    }

    [TestMethod]
    public async Task ListPackagesAsync_TamperedIndex_Throws()
    {
        var handler = new StubHttpHandler();
        var indexBytes = _fixture.PublishIndex(handler);

        // Change the served bytes after they were signed: the hash in the signature no longer matches.
        indexBytes[^3] ^= 0x20;

        var error = await Assert.ThrowsExactlyAsync<ModulePackageException>(
            () => CreateService(handler).ListPackagesAsync());

        StringAssert.Contains(error.Message, "SHA-256 of the served bytes");
    }

    [TestMethod]
    public async Task ListPackagesAsync_WithinTtl_UsesCache()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler);
        var service = CreateService(handler);

        await service.ListPackagesAsync();
        var requestsAfterFirst = handler.Requests.Count;
        await service.ListPackagesAsync();

        Assert.AreEqual(2, requestsAfterFirst, "index + signature are fetched once");
        Assert.AreEqual(requestsAfterFirst, handler.Requests.Count);
    }

    [TestMethod]
    public async Task ListPackagesAsync_AfterTtl_Refetches()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler);
        var service = CreateService(handler);

        await service.ListPackagesAsync();
        var requestsAfterFirst = handler.Requests.Count;

        _fixture.Time.Advance(TimeSpan.FromMinutes(61));
        await service.ListPackagesAsync();

        Assert.AreEqual(requestsAfterFirst * 2, handler.Requests.Count);
    }

    [TestMethod]
    public async Task ListPackagesAsync_RefreshFailure_ServesCachedIndexWithoutRetrying()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler);
        var service = CreateService(handler);

        await service.ListPackagesAsync();

        handler.Clear();
        _fixture.Time.Advance(TimeSpan.FromMinutes(61));

        var packages = await service.ListPackagesAsync();
        var requestsAfterFailure = handler.Requests.Count;
        await service.ListPackagesAsync();

        Assert.AreEqual(1, packages.Count);
        Assert.IsGreaterThan(0, requestsAfterFailure, "the refresh must be attempted once");
        Assert.AreEqual(
            requestsAfterFailure,
            handler.Requests.Count,
            "a dead store must not be retried on every call within the TTL");
    }

    [TestMethod]
    public async Task ListPackagesAsync_OlderGeneratedAt_Refused()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler, generatedAt: _fixture.Now);
        var service = CreateService(handler);

        await service.ListPackagesAsync();
        _fixture.Time.Advance(TimeSpan.FromMinutes(61));

        // A replay of an older catalogue must not silently roll the store back.
        _fixture.PublishIndex(
            handler,
            generatedAt: _fixture.Now.AddHours(-5),
            sha256Override: RemoteIndexFixture.OtherSha256,
            sizeOverride: 999);

        var packages = await service.ListPackagesAsync();

        Assert.AreEqual(1, packages.Count);
        Assert.AreEqual(
            RemoteIndexFixture.PackageSha256,
            packages[0].Sha256,
            "the cached (newer) index keeps being served");
    }

    [TestMethod]
    public async Task ListPackagesAsync_AllowIndexRollback_AcceptsOlderIndex()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler, generatedAt: _fixture.Now);
        var service = CreateService(
            handler, _fixture.CreateOptions(allowIndexRollback: true));

        await service.ListPackagesAsync();
        _fixture.Time.Advance(TimeSpan.FromMinutes(61));

        _fixture.PublishIndex(
            handler,
            generatedAt: _fixture.Now.AddHours(-5),
            sha256Override: RemoteIndexFixture.OtherSha256,
            sizeOverride: 999);

        var packages = await service.ListPackagesAsync();

        Assert.AreEqual(1, packages.Count);
        Assert.AreEqual(999, packages[0].SizeBytes, "the deliberately restored index is used");
    }

    [TestMethod]
    public async Task ListPackagesAsync_PrimaryFails_UsesMirror()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler, indexUrl: RemoteIndexFixture.MirrorUrl);

        var service = CreateService(
            handler, _fixture.CreateOptions(publishPrimary: false));

        var packages = await service.ListPackagesAsync();

        Assert.AreEqual(1, packages.Count);
        Assert.IsTrue(handler.Requests.Any(request => request.StartsWith(RemoteIndexFixture.MirrorUrl, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ResolveAsync_DownloadsPackageAndWritesSignature()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler);
        var service = CreateService(handler);

        var resolution = await service.ResolveAsync(ModuleId);

        Assert.IsNotNull(resolution);
        Assert.IsTrue(File.Exists(resolution.PackagePath));
        CollectionAssert.AreEqual(
            RemoteIndexFixture.PackageBytes,
            await File.ReadAllBytesAsync(resolution.PackagePath));

        Assert.IsNotNull(resolution.SignaturePath);
        var signature = ModulePackageSigner.Read(resolution.SignaturePath);
        Assert.AreEqual(_fixture.PublisherKey.KeyId, signature.KeyId);
        Assert.AreEqual(RemoteIndexFixture.PackageSha256, signature.Sha256);
        Assert.AreEqual(Path.GetFileName(resolution.PackagePath), signature.File);

        // The written signature must verify against the publisher key the index advertised.
        Assert.IsTrue(Ed25519Signature.Verify(
            Convert.FromHexString(signature.Sha256),
            _fixture.PublisherKey.PublicKey,
            signature.Signature));
    }

    [TestMethod]
    public async Task ResolveAsync_SecondCall_ReusesCachedPackage()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler);
        var service = CreateService(handler);

        var first = await service.ResolveAsync(ModuleId);
        var downloadsAfterFirst = handler.Requests.Count;
        var second = await service.ResolveAsync(ModuleId);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreEqual(downloadsAfterFirst, handler.Requests.Count);
        Assert.AreEqual(first.PackagePath, second.PackagePath);
    }

    [TestMethod]
    public async Task ResolveAsync_HashMismatch_ThrowsAndLeavesNoPackage()
    {
        var handler = new StubHttpHandler();

        // Same length, different bytes: the size check passes and the hash check must reject it.
        var tampered = Enumerable.Repeat((byte)'x', RemoteIndexFixture.PackageBytes.Length).ToArray();
        _fixture.PublishIndex(handler, servedPackageOverride: tampered);

        var error = await Assert.ThrowsExactlyAsync<ModulePackageException>(
            () => CreateService(handler).ResolveAsync(ModuleId));

        StringAssert.Contains(error.Message, "does not match the signed index");

        var packageDirectory = Path.Combine(_fixture.CachePath, "packages", ModuleId, "1.0.0");

        Assert.IsFalse(
            Directory.Exists(packageDirectory)
            && Directory.EnumerateFiles(packageDirectory, "*" + ModulePackageFormat.Extension).Any(),
            "a package that fails its hash check must never be left in the cache");
    }

    [TestMethod]
    public async Task ResolveAsync_AbsoluteDownloadUrlOnAnotherHost_Throws()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler, downloadUrl: "https://evil.test/org.example.receipts-1.0.0.dncpkg");

        var error = await Assert.ThrowsExactlyAsync<ModulePackageException>(
            () => CreateService(handler).ResolveAsync(ModuleId));

        StringAssert.Contains(error.Message, "DownloadOrigins");
    }

    [TestMethod]
    public async Task ResolveAsync_ParentTraversal_Throws()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler, downloadUrl: "../../org.example.receipts-1.0.0.dncpkg");

        var error = await Assert.ThrowsExactlyAsync<ModulePackageException>(
            () => CreateService(handler).ResolveAsync(ModuleId));

        StringAssert.Contains(error.Message, "escapes the store root");
    }

    [TestMethod]
    public async Task ResolveAsync_UnknownModule_ReturnsNull()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler);

        Assert.IsNull(await CreateService(handler).ResolveAsync("org.example.absent"));
    }

    [TestMethod]
    public async Task ResolveAsync_YankedVersion_IsReported()
    {
        var handler = new StubHttpHandler();
        _fixture.PublishIndex(handler, yanked: true, yankReason: "credential leak");
        var service = CreateService(handler);

        var resolution = await service.ResolveAsync(ModuleId);

        Assert.IsNotNull(resolution);
        Assert.IsTrue(resolution.Package.Yanked);
        Assert.AreEqual("credential leak", resolution.Package.YankReason);
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    /// <summary>Serves canned responses and records every request URL.</summary>
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _responses = new(StringComparer.Ordinal);

        public List<string> Requests { get; } = [];

        public void Map(string url, byte[] bytes) => _responses[url] = bytes;

        public void Clear()
        {
            _responses.Clear();
            Requests.Clear();
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add(url);

            if (!_responses.TryGetValue(url, out var bytes))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    RequestMessage = request,
                });
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(bytes),
            };

            response.Content.Headers.ContentLength = bytes.Length;
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            return Task.FromResult(response);
        }
    }

    /// <summary>A controllable clock.</summary>
    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; private set; } = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public void Advance(TimeSpan by) => Now += by;
    }

    /// <summary>Builds signed indices and maps them onto a <see cref="StubHttpHandler"/>.</summary>
    private sealed class RemoteIndexFixture : IDisposable
    {
        public const string MirrorUrl = "https://mirror.test/index/v1/index.json";

        public static readonly byte[] PackageBytes =
            Encoding.UTF8.GetBytes("pretend this is a deterministic package archive");

        public static readonly string PackageSha256 =
            Convert.ToHexStringLower(SHA256.HashData(PackageBytes));

        /// <summary>A hash no real package has, used to tell two index generations apart.</summary>
        public static readonly string OtherSha256 = new('b', 64);

        private readonly string _root;

        public RemoteIndexFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "dnc-remote-store-tests-" + Guid.CreateVersion7().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public ModuleSigningKeyPair StoreKey { get; } = Ed25519Signature.GenerateKeyPair("store-test-key");

        public ModuleSigningKeyPair PublisherKey { get; } = Ed25519Signature.GenerateKeyPair("publisher-test-key");

        public TestTimeProvider Time { get; } = new();

        public DateTimeOffset Now => Time.Now;

        public string CachePath => Path.Combine(_root, "cache");

        public ModuleStoreOptions CreateOptions(
            bool allowIndexRollback = false, bool publishPrimary = true) =>
            new()
            {
                IndexUrl = publishPrimary ? IndexUrl : "https://store.test/absent/index.json",
                Mirrors = [MirrorUrl],
                CachePath = CachePath,
                IndexCacheTtl = TimeSpan.FromHours(1),
                DownloadTimeout = TimeSpan.FromSeconds(10),
                AllowIndexRollback = allowIndexRollback,
                TrustedKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [StoreKey.KeyId] = StoreKey.PublicKey,
                },
            };

        /// <summary>Publishes the index (and its package) on the stub, returning the index bytes.</summary>
        public byte[] PublishIndex(
            StubHttpHandler handler,
            DateTimeOffset? generatedAt = null,
            string indexUrl = IndexUrl,
            ModuleSigningKeyPair? indexKey = null,
            string downloadUrl = PackageUrl,
            string? sha256Override = null,
            long? sizeOverride = null,
            byte[]? servedPackageOverride = null,
            bool yanked = false,
            string? yankReason = null)
        {
            var sha256 = sha256Override ?? PackageSha256;
            var size = sizeOverride ?? PackageBytes.Length;

            var json = BuildIndexJson(
                generatedAt ?? Now,
                downloadUrl,
                sha256,
                size,
                yanked,
                yankReason,
                Ed25519Signature.Sign(Convert.FromHexString(sha256), PublisherKey.PrivateKey));

            var indexBytes = Encoding.UTF8.GetBytes(json);
            var signature = SignatureFor(indexBytes, indexKey ?? StoreKey);

            handler.Map(indexUrl, indexBytes);
            handler.Map(indexUrl + ModulePackageFormat.SignatureExtension, Serialize(signature));
            handler.Map("https://store.test/" + downloadUrl, servedPackageOverride ?? PackageBytes);

            if (!string.Equals(indexUrl, MirrorUrl, StringComparison.Ordinal))
            {
                handler.Map(MirrorUrl, indexBytes);
                handler.Map(MirrorUrl + ModulePackageFormat.SignatureExtension, Serialize(signature));
            }

            return indexBytes;
        }

        private static byte[] Serialize(ModulePackageSignatureFile signature) =>
            Encoding.UTF8.GetBytes(ModulePackageSigner.Serialize(signature));

        private static ModulePackageSignatureFile SignatureFor(byte[] bytes, ModuleSigningKeyPair key)
        {
            var digest = SHA256.HashData(bytes);

            return new ModulePackageSignatureFile
            {
                KeyId = key.KeyId,
                File = "index.json",
                Sha256 = Convert.ToHexStringLower(digest),
                Signature = Ed25519Signature.Sign(digest, key.PrivateKey),
            };
        }

        private string BuildIndexJson(
            DateTimeOffset generatedAt,
            string downloadUrl,
            string sha256,
            long size,
            bool yanked,
            string? yankReason,
            string packageSignature)
        {
            var yankFields = yanked
                ? $",\n                          \"yanked\": true,\n                          \"yankReason\": \"{yankReason}\""
                : ",\n                          \"yanked\": false";

            return $$"""
                {
                  "schemaVersion": 1,
                  "generatedAt": "{{generatedAt:O}}",
                  "packages": [
                    {
                      "id": "{{ModuleId}}",
                      "name": "Receipts",
                      "publisher": { "id": "example-org", "name": "Example Org", "verified": true },
                      "versions": [
                        {
                          "version": "1.0.0",
                          "moduleApiVersion": 1,
                          "dataStore": "isolated",
                          "capabilities": ["IAuditLogger", "IStorageProvider"],
                          "userConsentScopes": [],
                          "publishedAt": "{{generatedAt:O}}",
                          "downloadUrl": "{{downloadUrl}}",
                          "size": {{size}},
                          "sha256": "{{sha256}}",
                          "signature": {
                            "keyId": "{{PublisherKey.KeyId}}",
                            "value": "{{packageSignature}}"
                          }{{yankFields}}
                        }
                      ]
                    }
                  ]
                }
                """;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch (IOException)
            {
                // A leftover temp directory is harmless.
            }
        }
    }
}
