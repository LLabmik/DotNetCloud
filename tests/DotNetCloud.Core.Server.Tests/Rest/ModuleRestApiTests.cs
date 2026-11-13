using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Grpc.Interceptors;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Rest;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Server.Supervisor;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Rest;

/// <summary>
/// Tests for the internal module REST facade: it must authenticate with the module credential,
/// derive the caller from the core-signed assertion, and enforce the same capability decision as the
/// gRPC listener — without carrying a second copy of those rules.
/// </summary>
[TestClass]
public class ModuleRestApiTests
{
    private const string ModuleId = "dotnetcloud.files";

    [TestMethod]
    public async Task ListFiles_Granted_ReturnsTheFolderAsJson()
    {
        var harness = CreateHarness();
        var userId = Guid.CreateVersion7();
        harness.Identity.UserId = userId;

        harness.Files
            .Setup(client => client.ListChildrenAsync(userId, null, 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DotNetCloud.Core.Services.ModuleApis.FileListResult
            {
                Status = DotNetCloud.Core.Services.ModuleApis.FilesAccessStatus.Success,
                TotalCount = 1,
                Nodes =
                [
                    new FileNodeInfo
                    {
                        Id = Guid.CreateVersion7(),
                        Name = "Report.pdf",
                        NodeType = "File",
                        MimeType = "application/pdf",
                        Size = 10,
                    },
                ],
            });

        var http = CreateAuthenticatedHttp(harness, harness.KeyRing.Mint(userId, ModuleId));

        var (status, body) = await ExecuteAsync(await harness.Api.ListFilesAsync(http, null, null, null), http);

        Assert.AreEqual(StatusCodes.Status200OK, status);
        using var json = JsonDocument.Parse(body);
        Assert.AreEqual(1, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.AreEqual(
            "Report.pdf",
            json.RootElement.GetProperty("nodes")[0].GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task ListFiles_CapabilityNotGranted_Returns403AndNeverCallsFiles()
    {
        var harness = CreateHarness(allowed: false);
        var userId = Guid.CreateVersion7();
        harness.Identity.UserId = userId;
        var http = CreateAuthenticatedHttp(harness, harness.KeyRing.Mint(userId, ModuleId));

        var (status, body) = await ExecuteAsync(await harness.Api.ListFilesAsync(http, null, null, null), http);

        Assert.AreEqual(StatusCodes.Status403Forbidden, status);
        StringAssert.Contains(body, "CAP_NOT_GRANTED");
        harness.Files.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task GetUser_MissingCredential_Returns401()
    {
        var harness = CreateHarness();
        var http = CreateHttp(moduleId: null);

        var (status, body) = await ExecuteAsync(
            await harness.Api.GetUserAsync(http, Guid.CreateVersion7().ToString()), http);

        Assert.AreEqual(StatusCodes.Status401Unauthorized, status);
        StringAssert.Contains(body, "AUTH_UNAUTHORIZED");
    }

    [TestMethod]
    public async Task GetUser_InvalidCredential_Returns401()
    {
        var harness = CreateHarness();
        harness.Credentials.Issue(ModuleId);
        var http = CreateHttp(token: "not-the-issued-token");

        var (status, body) = await ExecuteAsync(
            await harness.Api.GetUserAsync(http, Guid.CreateVersion7().ToString()), http);

        Assert.AreEqual(StatusCodes.Status401Unauthorized, status);
        Assert.AreEqual(AuditAction.ModuleAuthFailed, harness.Audit.SingleEntry().Action);
        _ = body;
    }

    [TestMethod]
    public async Task GetUser_NonAsciiModuleIdHeader_IsTreatedAsMissing()
    {
        // Metadata only accepts ASCII; a hostile header must not turn authentication into a 500.
        var harness = CreateHarness();
        var http = CreateHttp(moduleId: "dotnetcloud.files\u00e9", token: "irrelevant");

        var (status, _) = await ExecuteAsync(
            await harness.Api.GetUserAsync(http, Guid.CreateVersion7().ToString()), http);

        Assert.AreEqual(StatusCodes.Status401Unauthorized, status);
    }

    [TestMethod]
    public async Task GetUser_CapabilityNotGranted_Returns403WithStableCode()
    {
        var harness = CreateHarness(allowed: false);
        var http = CreateAuthenticatedHttp(harness);
        var userId = Guid.CreateVersion7();

        var (status, body) = await ExecuteAsync(await harness.Api.GetUserAsync(http, userId.ToString()), http);

        Assert.AreEqual(StatusCodes.Status403Forbidden, status);
        StringAssert.Contains(body, "CAP_NOT_GRANTED");
        harness.Teams.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task GetUser_Granted_ReturnsMinimalProfile()
    {
        var harness = CreateHarness();
        var http = CreateAuthenticatedHttp(harness);
        var userId = Guid.CreateVersion7();

        harness.Users
            .Setup(directory => directory.GetDisplayNamesAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, string>)
                new Dictionary<Guid, string> { [userId] = "Alice" });

        harness.Users
            .Setup(directory => directory.GetAvatarUrlsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string>());

        var (status, body) = await ExecuteAsync(await harness.Api.GetUserAsync(http, userId.ToString()), http);

        Assert.AreEqual(StatusCodes.Status200OK, status);
        using var json = JsonDocument.Parse(body);
        Assert.IsTrue(json.RootElement.GetProperty("found").GetBoolean());
        Assert.AreEqual("Alice", json.RootElement.GetProperty("user").GetProperty("displayName").GetString());
    }

    [TestMethod]
    public async Task GetTeamsForUser_AssertionForAnotherUser_Returns403SubjectMismatch()
    {
        var harness = CreateHarness();
        var caller = Guid.CreateVersion7();
        var victim = Guid.CreateVersion7();
        harness.Identity.UserId = caller;

        var http = CreateAuthenticatedHttp(harness, assertion: harness.KeyRing.Mint(caller, ModuleId));

        var (status, body) = await ExecuteAsync(
            await harness.Api.GetTeamsForUserAsync(http, victim.ToString()), http);

        Assert.AreEqual(StatusCodes.Status403Forbidden, status);
        StringAssert.Contains(body, "AUTH_SUBJECT_MISMATCH");
        harness.Teams.Verify(
            directory => directory.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetTeamsForUser_AssertionForAnotherModule_Returns401()
    {
        var harness = CreateHarness();
        var caller = Guid.CreateVersion7();
        harness.Identity.UserId = caller;

        var http = CreateAuthenticatedHttp(
            harness,
            assertion: harness.KeyRing.Mint(caller, "dotnetcloud.chat"));

        var (status, _) = await ExecuteAsync(
            await harness.Api.GetTeamsForUserAsync(http, caller.ToString()), http);

        Assert.AreEqual(StatusCodes.Status401Unauthorized, status);
    }

    [TestMethod]
    public async Task SendNotification_ExternalLink_Returns400WithModuleInvalidRequest()
    {
        var harness = CreateHarness();
        var http = CreateAuthenticatedHttp(harness);

        var request = new ModuleRestContracts.NotificationRequest(
            "Hi",
            "body",
            null,
            "https://evil.example/login",
            [Guid.CreateVersion7().ToString()]);

        var (status, body) = await ExecuteAsync(
            await harness.Api.SendNotificationAsync(http, request), http);

        Assert.AreEqual(StatusCodes.Status400BadRequest, status);
        StringAssert.Contains(body, "MODULE_INVALID_REQUEST");
    }

    [TestMethod]
    public async Task GetHealth_ValidCredential_ReturnsModuleId()
    {
        var harness = CreateHarness();
        var http = CreateAuthenticatedHttp(harness);

        var (status, body) = await ExecuteAsync(await harness.Api.GetHealthAsync(http), http);

        Assert.AreEqual(StatusCodes.Status200OK, status);
        using var json = JsonDocument.Parse(body);
        Assert.AreEqual(ModuleId, json.RootElement.GetProperty("moduleId").GetString());
    }

    [TestMethod]
    public async Task ConsentEndpoints_WithoutConsent_ReturnEmptyScopeSet()
    {
        var harness = CreateHarness();
        var http = CreateAuthenticatedHttp(harness);

        // The canonical surface now answers the consent queries instead of 501: a module with no
        // declared scopes and no asserted user gets an empty (not unknown) answer.
        var (status, body) = await ExecuteAsync(
            await harness.Api.GetGrantedScopesAsync(http), http);

        Assert.AreEqual(StatusCodes.Status200OK, status);
        StringAssert.Contains(body, "\"scopes\"");
    }

    private sealed class Harness
    {
        public ModuleRestApiService Api { get; init; } = null!;
        public ModuleCredentialStore Credentials { get; init; } = null!;
        public ModuleAssertionKeyRing KeyRing { get; init; } = null!;
        public RecordingAuditLogger Audit { get; init; } = null!;
        public StubIdentityResolver Identity { get; init; } = null!;
        public Mock<IUserDirectory> Users { get; init; } = null!;
        public Mock<ITeamDirectory> Teams { get; init; } = null!;
        public Mock<DotNetCloud.Core.Services.ModuleApis.IFilesApiClient> Files { get; init; } = null!;
    }

    private static Harness CreateHarness(bool allowed = true)
    {
        var credentials = new ModuleCredentialStore();
        var keyRing = new ModuleAssertionKeyRing();
        var audit = new RecordingAuditLogger();
        var identity = new StubIdentityResolver();
        var users = new Mock<IUserDirectory>();
        var teams = new Mock<ITeamDirectory>();
        var files = new Mock<DotNetCloud.Core.Services.ModuleApis.IFilesApiClient>();

        var services = new ServiceCollection();
        services.AddSingleton<IUserDirectory>(users.Object);
        services.AddSingleton<ITeamDirectory>(teams.Object);
        services.AddSingleton<IAuditLogger>(audit);
        services.AddScoped(_ => files.Object);
        var provider = services.BuildServiceProvider();

        var legacy = new CoreCapabilitiesServiceImpl(
            NullLogger<CoreCapabilitiesServiceImpl>.Instance,
            provider,
            new SearchIndexingService(
                Mock.Of<IServiceScopeFactory>(),
                NullLogger<SearchIndexingService>.Instance),
            new ModuleEventInbox(
                Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
                Mock.Of<DotNetCloud.Core.Server.ModuleLoading.IModuleManifestRegistry>(),
                NullLogger<ModuleEventInbox>.Instance),
            ConsentTestDoubles.NoConsent());

        var api = new ModuleApiV1Service(
            legacy,
            new ModuleHealthRegistry(TimeProvider.System),
            ConsentTestDoubles.NoConsent(),
            new DotNetCloud.Core.Server.Services.ModuleFileAccessService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                ConsentTestDoubles.NoConsent(),
                NullLogger<DotNetCloud.Core.Server.Services.ModuleFileAccessService>.Instance),
            NullLogger<ModuleApiV1Service>.Instance);

        var evaluator = new Mock<IModuleAccessEvaluator>();
        evaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => allowed
                ? ModuleAccessDecision.Allow("IUserDirectory")
                : ModuleAccessDecision.Deny("IUserDirectory", ModuleAccessDenialReason.CapabilityNotGranted));

        // Quota resolution falls back to the policy defaults when the database is unavailable,
        // which is exactly what a throwing scope factory simulates here.
        var quotaCache = new ModuleQuotaCache(
            new ModuleQuotaResolver(
                new ThrowingScopeFactory(),
                Options.Create(new ModuleQuotasOptions()),
                Options.Create(new ProcessSupervisorOptions())),
            TimeProvider.System);

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new Harness
        {
            Credentials = credentials,
            KeyRing = keyRing,
            Audit = audit,
            Identity = identity,
            Users = users,
            Teams = teams,
            Files = files,
            Api = new ModuleRestApiService(
                api,
                new AuthenticationInterceptor(
                    NullLogger<AuthenticationInterceptor>.Instance,
                    credentials,
                    scopeFactory,
                    TimeProvider.System),
                new CallerContextInterceptor(
                    NullLogger<CallerContextInterceptor>.Instance,
                    identity,
                    keyRing),
                new ModuleRateLimitInterceptor(
                    quotaCache,
                    new ModuleRateLimiter(TimeProvider.System),
                    NullLogger<ModuleRateLimitInterceptor>.Instance),
                new ModuleAccessInterceptor(
                    evaluator.Object,
                    scopeFactory,
                    NullLogger<ModuleAccessInterceptor>.Instance),
                new DotNetCloud.Core.Server.Services.ModuleFileAccessService(
                    scopeFactory,
                    ConsentTestDoubles.NoConsent(),
                    NullLogger<DotNetCloud.Core.Server.Services.ModuleFileAccessService>.Instance)),
        };
    }

    private static DefaultHttpContext CreateAuthenticatedHttp(Harness harness, string? assertion = null)
    {
        var token = harness.Credentials.Issue(ModuleId);
        return CreateHttp(token: token, assertion: assertion);
    }

    private static readonly IServiceProvider HttpServices = new ServiceCollection()
        .AddLogging()
        .AddOptions()
        .BuildServiceProvider();

    private static DefaultHttpContext CreateHttp(
        string? moduleId = ModuleId,
        string? token = null,
        string? assertion = null)
    {
        var http = new DefaultHttpContext
        {
            RequestServices = HttpServices,
        };
        http.Response.Body = new MemoryStream();
        http.Request.Path = "/internal/moduleapi/v1/test";

        if (moduleId is not null)
        {
            http.Request.Headers[ModuleIdentityMetadata.ModuleIdHeader] = moduleId;
        }

        if (token is not null)
        {
            http.Request.Headers[ModuleIdentityMetadata.ModuleTokenHeader] = token;
        }

        if (assertion is not null)
        {
            http.Request.Headers[ModuleIdentityMetadata.UserAssertionHeader] = assertion;
        }

        return http;
    }

    private static async Task<(int Status, string Body)> ExecuteAsync(IResult result, DefaultHttpContext http)
    {
        await result.ExecuteAsync(http);

        http.Response.Body.Position = 0;
        using var reader = new StreamReader(http.Response.Body);
        return (http.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private sealed class StubIdentityResolver : ICallerIdentityResolver
    {
        public Guid UserId { get; set; }

        public Task<IReadOnlyList<string>?> ResolveRolesAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>?>(
                UserId != Guid.Empty && userId == UserId ? new[] { "User" } : null);
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public AuditEntry SingleEntry()
        {
            Assert.AreEqual(1, Entries.Count);
            return Entries[0];
        }

        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("no database in this test");
    }
}
