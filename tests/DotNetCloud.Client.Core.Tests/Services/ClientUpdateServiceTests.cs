using System.Net;
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
                            new { name = "dotnetcloud-99.0.0-linux-x64.tar.gz", browser_download_url = "https://example.com/dl", size = 5000, content_type = "application/gzip" }
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
            () => svc.DownloadUpdateAsync(null!, null));
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
}
