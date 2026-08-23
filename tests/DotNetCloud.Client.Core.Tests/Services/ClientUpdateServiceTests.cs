using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNetCloud.Client.Core.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace DotNetCloud.Client.Core.Tests.Services;

[TestClass]
public sealed class ClientUpdateServiceTests
{
    // ── CheckForUpdate: server endpoint ───────────────────────────────────

    [TestMethod]
    public async Task CheckForUpdateAsync_ServerReturnsUpdate_ReturnsUpdateAvailable()
    {
        var serverResponse = new
        {
            success = true,
            data = new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                CurrentVersion = "0.1.7-alpha",
                LatestVersion = "0.2.0",
                ReleaseUrl = "https://github.com/LLabmik/DotNetCloud/releases/tag/v0.2.0",
                ReleaseNotes = "New features",
                PublishedAt = DateTimeOffset.UtcNow,
                Assets = [new ReleaseAsset { Name = "dotnetcloud-0.2.0-linux-x64.tar.gz", DownloadUrl = "https://example.com/download", Size = 1000, Platform = "linux-x64" }],
            }
        };

        var handler = CreateMockHandler(JsonSerializer.Serialize(serverResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://cloud.example.com/") };
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        var result = await svc.CheckForUpdateAsync();

        Assert.IsTrue(result.IsUpdateAvailable);
        Assert.AreEqual("0.2.0", result.LatestVersion);
        Assert.AreEqual(1, result.Assets.Count);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_ServerReturnsNoUpdate_ReturnsNotAvailable()
    {
        var serverResponse = new
        {
            success = true,
            data = new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = "0.1.7-alpha",
                LatestVersion = "0.1.7-alpha",
                Assets = Array.Empty<ReleaseAsset>(),
            }
        };

        var handler = CreateMockHandler(JsonSerializer.Serialize(serverResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://cloud.example.com/") };
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        var result = await svc.CheckForUpdateAsync();

        Assert.IsFalse(result.IsUpdateAvailable);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_ServerFails_FallsBackToGitHub()
    {
        // Server returns 500, GitHub returns a valid release.
        var callCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                callCount++;
                if (req.RequestUri?.Host == "cloud.example.com")
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                // GitHub response.
                var releases = new[]
                {
                    new
                    {
                        tag_name = "v99.0.0",
                        html_url = "https://github.com/LLabmik/DotNetCloud/releases/tag/v99.0.0",
                        body = "Huge update",
                        published_at = DateTimeOffset.UtcNow.ToString("o"),
                        prerelease = false,
                        assets = new[]
                        {
                            new { name = "dotnetcloud-desktop-client-linux-x64-99.0.0.tar.gz", browser_download_url = "https://example.com/dl", size = 5000, content_type = "application/gzip" }
                        }
                    }
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(releases, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
                };
            });

        var http = new HttpClient(handler.Object) { BaseAddress = new Uri("https://cloud.example.com/") };
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        var result = await svc.CheckForUpdateAsync();

        Assert.IsTrue(result.IsUpdateAvailable);
        Assert.AreEqual("99.0.0", result.LatestVersion);
        Assert.IsTrue(callCount >= 2, "Should have tried server then GitHub.");
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_NoBaseAddress_SkipsServerAndHitsGitHub()
    {
        var releases = new[]
        {
            new
            {
                tag_name = "v99.0.0",
                html_url = "https://github.com/LLabmik/DotNetCloud/releases/tag/v99.0.0",
                body = "Huge update",
                published_at = DateTimeOffset.UtcNow.ToString("o"),
                prerelease = false,
                assets = Array.Empty<object>()
            }
        };

        var handler = CreateMockHandler(JsonSerializer.Serialize(releases, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var http = new HttpClient(handler); // No BaseAddress.
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        var result = await svc.CheckForUpdateAsync();

        Assert.IsTrue(result.IsUpdateAvailable);
    }

    // ── UpdateAvailable event ─────────────────────────────────────────────

    [TestMethod]
    public async Task CheckForUpdateAsync_WhenUpdateAvailable_RaisesEvent()
    {
        var serverResponse = new
        {
            success = true,
            data = new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                CurrentVersion = "0.1.7",
                LatestVersion = "1.0.0",
                Assets = Array.Empty<ReleaseAsset>(),
            }
        };

        var handler = CreateMockHandler(JsonSerializer.Serialize(serverResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://cloud.example.com/") };
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        UpdateCheckResult? eventResult = null;
        svc.UpdateAvailable += (_, r) => eventResult = r;

        await svc.CheckForUpdateAsync();

        Assert.IsNotNull(eventResult);
        Assert.IsTrue(eventResult!.IsUpdateAvailable);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_WhenNoUpdate_DoesNotRaiseEvent()
    {
        var serverResponse = new
        {
            success = true,
            data = new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = "0.1.7",
                LatestVersion = "0.1.7",
                Assets = Array.Empty<ReleaseAsset>(),
            }
        };

        var handler = CreateMockHandler(JsonSerializer.Serialize(serverResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://cloud.example.com/") };
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        bool eventFired = false;
        svc.UpdateAvailable += (_, _) => eventFired = true;

        await svc.CheckForUpdateAsync();

        Assert.IsFalse(eventFired);
    }

    // ── GitHub fallback: asset platform inference ─────────────────────────

    [TestMethod]
    public async Task CheckForUpdateAsync_GitHubFallback_FiltersToDesktopClientAssets()
    {
        // When falling back to GitHub, only "desktop-client" assets should
        // get a non-null platform. Server-only tarballs should be excluded.
        var releases = new[]
        {
            new
            {
                tag_name = "v99.0.0",
                html_url = "https://github.com/LLabmik/DotNetCloud/releases/tag/v99.0.0",
                body = "Release",
                published_at = DateTimeOffset.UtcNow.ToString("o"),
                prerelease = false,
                assets = new object[]
                {
                    new { name = "dotnetcloud-99.0.0-linux-x64.tar.gz",              browser_download_url = "https://example.com/srv", size = 5000, content_type = "application/gzip" },
                    new { name = "dotnetcloud-desktop-client-linux-x64-99.0.0.tar.gz", browser_download_url = "https://example.com/cli", size = 3000, content_type = "application/gzip" },
                    new { name = "dotnetcloud-desktop-client-win-x64-99.0.0.zip",       browser_download_url = "https://example.com/win", size = 2000, content_type = "application/zip" },
                }
            }
        };

        var handler = CreateMockHandler(JsonSerializer.Serialize(releases, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var http = new HttpClient(handler); // No BaseAddress → forces GitHub path.
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        var result = await svc.CheckForUpdateAsync();

        Assert.IsTrue(result.IsUpdateAvailable);
        Assert.AreEqual("99.0.0", result.LatestVersion);

        // The server-only asset should have no platform.
        var srvAsset = result.Assets.FirstOrDefault(a => a.Name.Contains("dotnetcloud-99.0.0-linux-x64"));
        Assert.IsNotNull(srvAsset);
        Assert.IsNull(srvAsset!.Platform, "Server-only asset should have null platform.");

        // Desktop-client assets should have their platform inferred.
        var linuxAsset = result.Assets.FirstOrDefault(a => a.Name.Contains("desktop-client-linux-x64"));
        Assert.IsNotNull(linuxAsset);
        Assert.AreEqual("linux-x64", linuxAsset!.Platform);

        var winAsset = result.Assets.FirstOrDefault(a => a.Name.Contains("desktop-client-win-x64"));
        Assert.IsNotNull(winAsset);
        Assert.AreEqual("win-x64", winAsset!.Platform);
    }

    // ── DownloadUpdateAsync ───────────────────────────────────────────────

    [TestMethod]
    public async Task DownloadUpdateAsync_WritesFileAndReportsProgress()
    {
        var content = new byte[1024];
        new Random(42).NextBytes(content);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
                {
                    Headers = { ContentLength = content.Length }
                }
            });

        var http = new HttpClient(handler.Object);
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        var progressValues = new List<double>();
        var asset = new ReleaseAsset
        {
            Name = "test-update.tar.gz",
            DownloadUrl = "https://example.com/test-update.tar.gz",
            Size = content.Length,
        };

        var path = await svc.DownloadUpdateAsync(asset, new Progress<double>(p => progressValues.Add(p)));

        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(content.Length, new FileInfo(path).Length);

        // Clean up.
        File.Delete(path);
    }

    [TestMethod]
    public async Task DownloadUpdateAsync_NullAsset_Throws()
    {
        var http = new HttpClient();
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => svc.DownloadUpdateAsync(null!, (IProgress<double>?)null));
    }

    [TestMethod]
    public async Task DownloadUpdateAsync_ToDestination_WritesFileAndReportsDetailedProgress()
    {
        var content = new byte[2048];
        new Random(7).NextBytes(content);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
                {
                    Headers = { ContentLength = content.Length }
                }
            });

        var http = new HttpClient(handler.Object);
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        var destDir = Path.Combine(Path.GetTempPath(), "DotNetCloud", "updates", "test-" + Guid.NewGuid().ToString("N"));
        var snapshots = new List<DownloadProgress>();
        var asset = new ReleaseAsset
        {
            Name = "dotnetcloud-desktop-client-linux-x64-1.2.3.tar.gz",
            DownloadUrl = "https://example.com/update.tar.gz",
            Size = content.Length,
        };

        var result = await svc.DownloadUpdateAsync(
            asset, destDir, new Progress<DownloadProgress>(snapshots.Add));

        // Progress<T> delivers callbacks asynchronously (it falls back to the
        // thread pool when no SynchronizationContext is present), so the final
        // snapshots may not have arrived yet when DownloadUpdateAsync returns.
        // Wait (bounded) for the terminal 1.0 snapshot before asserting — same
        // pattern as MetadataEnrichmentServiceTests. Without this, the test
        // flakes under CI's parallel test load: Assert.IsTrue(snapshots.Count > 0)
        // can observe an empty list right after the download completes.
        var snapshotDeadline = DateTime.UtcNow.AddSeconds(5);
        while ((snapshots.Count == 0 || snapshots[^1].Percent < 1.0) &&
               DateTime.UtcNow < snapshotDeadline)
        {
            await Task.Delay(10);
        }

        Assert.IsTrue(File.Exists(result.FilePath));
        Assert.AreEqual(content.Length, result.SizeBytes);
        Assert.IsTrue(result.FilePath.StartsWith(destDir, StringComparison.Ordinal));
        Assert.IsFalse(result.Sha256Verified);
        Assert.IsTrue(snapshots.Count > 0);
        Assert.IsTrue(snapshots.Any(p => p.BytesDownloaded > 0));
        Assert.AreEqual(1.0, snapshots[^1].Percent);

        File.Delete(result.FilePath);
        Directory.Delete(destDir, recursive: true);
    }

    [TestMethod]
    public async Task DownloadUpdateAsync_ReadFails_DeletesPartialFile()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new ThrowingReadStream())
            });

        var http = new HttpClient(handler.Object);
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        var destDir = Path.Combine(Path.GetTempPath(), "DotNetCloud", "updates", "test-" + Guid.NewGuid().ToString("N"));
        var asset = new ReleaseAsset
        {
            Name = "cancel-me.tar.gz",
            DownloadUrl = "https://example.com/cancel-me.tar.gz",
            Size = 1024,
        };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => svc.DownloadUpdateAsync(asset, destDir, progress: null));

        Assert.IsFalse(File.Exists(Path.Combine(destDir, "cancel-me.tar.gz")));
        Directory.Delete(destDir, recursive: true);
    }

    [TestMethod]
    public async Task DownloadUpdateAsync_WithMatchingChecksum_Verifies()
    {
        var content = Encoding.UTF8.GetBytes("hello update");
        var expected = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
                {
                    Headers = { ContentLength = content.Length }
                }
            });

        var http = new HttpClient(handler.Object);
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        var destDir = Path.Combine(Path.GetTempPath(), "DotNetCloud", "updates", "test-" + Guid.NewGuid().ToString("N"));
        var asset = new ReleaseAsset
        {
            Name = "verified.tar.gz",
            DownloadUrl = "https://example.com/verified.tar.gz",
            Size = content.Length,
            Sha256Checksum = "sha256:" + expected,
        };

        var result = await svc.DownloadUpdateAsync(asset, destDir, progress: null);

        Assert.IsTrue(result.Sha256Verified);

        File.Delete(result.FilePath);
        Directory.Delete(destDir, recursive: true);
    }

    [TestMethod]
    public async Task DownloadUpdateAsync_WithMismatchedChecksum_ThrowsAndDeletesFile()
    {
        var content = Encoding.UTF8.GetBytes("hello update");

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
                {
                    Headers = { ContentLength = content.Length }
                }
            });

        var http = new HttpClient(handler.Object);
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        var destDir = Path.Combine(Path.GetTempPath(), "DotNetCloud", "updates", "test-" + Guid.NewGuid().ToString("N"));
        var asset = new ReleaseAsset
        {
            Name = "bad-checksum.tar.gz",
            DownloadUrl = "https://example.com/bad-checksum.tar.gz",
            Size = content.Length,
            Sha256Checksum = "sha256:" + new string('0', 64),
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => svc.DownloadUpdateAsync(asset, destDir, progress: null));

        Assert.IsFalse(File.Exists(Path.Combine(destDir, "bad-checksum.tar.gz")));
        Directory.Delete(destDir, recursive: true);
    }

    // ── ApplyUpdateAsync ──────────────────────────────────────────────────

    [TestMethod]
    public async Task ApplyUpdateAsync_MissingFile_Throws()
    {
        var http = new HttpClient();
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(
            () => svc.ApplyUpdateAsync("/nonexistent/file.tar.gz"));
    }

    [TestMethod]
    public async Task ApplyUpdateAsync_EmptyPath_Throws()
    {
        var http = new HttpClient();
        var svc = new ClientUpdateService(http, NullLogger<ClientUpdateService>.Instance);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => svc.ApplyUpdateAsync(string.Empty));
    }

    // ── Test helpers ──────────────────────────────────────────────────────

    private static HttpMessageHandler CreateMockHandler(string responseJson)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        return handler.Object;
    }

    /// <summary>
    /// A read stream that fails immediately with <see cref="OperationCanceledException"/>,
    /// used to exercise partial-download cleanup without timing-dependent delays.
    /// </summary>
    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new OperationCanceledException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromException<int>(new OperationCanceledException(cancellationToken));
    }
}
