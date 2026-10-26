extern alias FilesHost;

using System.Security.Claims;
using System.Text.Encodings.Web;
using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.Files.Data;
using FilesHost::DotNetCloud.Modules.Files.Host;
using FilesHost::DotNetCloud.Modules.Files.Host.Protos;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// Integration test host for the standalone Files module process.
/// </summary>
internal sealed class FilesHostWebApplicationFactory : WebApplicationFactory<FilesHost::Program>
{
    /// <summary>
    /// The credential the in-process host expects on every gRPC call. In production the process
    /// supervisor mints this per launch; the test host has no supervisor, so it is fixed here and
    /// presented by the test clients — there is deliberately no server-side bypass.
    /// </summary>
    public const string TestModuleSecret = "integration-test-module-secret";

    private readonly string _databaseName = $"FilesHostInt_{Guid.CreateVersion7():N}";

    public FilesHostWebApplicationFactory()
    {
        // The Files host reads connectionString/databaseProvider from builder.Configuration
        // at the very top of Program.Main (top-level statements). WebApplicationFactory's
        // ConfigureAppConfiguration is applied after top-level statements run, so it can't
        // satisfy the host's fail-fast DB guard. Provide the values via environment variables
        // instead (WebApplicationBuilder loads env vars into configuration automatically).
        // The DbContexts are replaced with InMemory below, so this is never used.
        Environment.SetEnvironmentVariable("connectionString", "Host=localhost;Database=integration_test;Username=test;Password=test");
        Environment.SetEnvironmentVariable("databaseProvider", "PostgreSql");

        // The module peer-authentication interceptor reads its credential from this variable and
        // fails closed without it.
        Environment.SetEnvironmentVariable(
            ModuleIdentityMetadata.ModuleSecretEnvironmentVariable,
            TestModuleSecret);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace FilesDbContext with an in-memory database scoped to this test run.
            // Use manual registration (not AddDbContext) to avoid registering a second
            // database provider in the DI container — EF Core rejects dual providers.
            services.RemoveAll<DbContextOptions<FilesDbContext>>();
            services.RemoveAll<FilesDbContext>();
            services.AddSingleton<DbContextOptions<FilesDbContext>>(_ =>
                new DbContextOptionsBuilder<FilesDbContext>()
                    .UseInMemoryDatabase(_databaseName)
                    .Options);
            services.AddScoped<FilesDbContext>();

            // The Files host also registers a transient CoreDbContext (for querying identity
            // tables during group validation). Replace it with InMemory too so it never hits
            // the real database.
            services.RemoveAll<CoreDbContext>();
            services.AddSingleton<DbContextOptions<CoreDbContext>>(_ =>
                new DbContextOptionsBuilder<CoreDbContext>()
                    .UseInMemoryDatabase(_databaseName)
                    .Options);
            services.AddScoped<CoreDbContext>();

            // Every Files host in this assembly shares one on-disk store (<content root>/storage,
            // because Files:StoragePath and DOTNETCLOUD_DATA_DIR are not overridden) while each
            // factory keeps its own in-memory database. MSTest runs test classes in parallel, so
            // a housekeeping service started by one host would happily delete chunks or blobs
            // another host just wrote — that is exactly how
            // FilesThumbnailIntegrationTests.GetThumbnail_WhenGenerated_ReturnsJpegContent started
            // returning 404 ("whole-file blob ... is missing from storage"). Those services are
            // meaningless here because the in-memory database is never the authority for the
            // shared store, so drop the ones whose only job is reclaiming storage.
            RemoveStorageReclaimingHostedServices(services);

            // Inject a deterministic test identity from request header for auth-bound gRPC checks.
            services.AddSingleton<IStartupFilter, TestUserStartupFilter>();

            // Register test auth handler for the auth schemes
            // required by FilesControllerBase [Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict...")].
            // The host's Program.cs already registers Identity.Application via AddCookie,
            // so we forward it instead of re-registering (which would throw "Scheme already exists").
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationSchemeOptions>(
                "OpenIddict.Validation.AspNetCore", opts =>
                {
                    opts.ForwardDefault = TestAuthHandler.SchemeName;
                });

            services.PostConfigure<CookieAuthenticationOptions>(
                IdentityConstants.ApplicationScheme, opts =>
                {
                    opts.ForwardDefaultSelector = context =>
                        context.Request.Headers.ContainsKey("x-test-user-id")
                            ? TestAuthHandler.SchemeName
                            : null;
                });

            // Register authorization policies and handler used by admin-only endpoints.
            services.AddAuthorizationBuilder()
                .AddPolicy("RequireAdmin", policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement("admin")));
            services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        });
    }

    /// <summary>
    /// Removes the hosted services registered by the Files module that delete files from the
    /// shared on-disk store.
    /// </summary>
    /// <remarks>
    /// The service types are internal to <c>DotNetCloud.Modules.Files.Data</c>, so they are matched
    /// by implementation type name. Keep this list in sync with
    /// <c>FilesServiceRegistration.AddFilesServices</c>.
    /// </remarks>
    /// <param name="services">Host service collection.</param>
    private static void RemoveStorageReclaimingHostedServices(IServiceCollection services)
    {
        string[] storageReclaimers =
        [
            "TempFileCleanupService",
            "TrashCleanupService",
            "UploadSessionCleanupService",
            "VersionCleanupService",
            "WholeFileBlobSweepService",
        ];

        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType is { } implementationType
                && storageReclaimers.Contains(implementationType.Name, StringComparer.Ordinal))
            {
                services.RemoveAt(i);
            }
        }
    }

    public FilesService.FilesServiceClient CreateFilesClient(Guid authenticatedUserId)
    {
        var client = CreateDefaultClient();
        client.DefaultRequestHeaders.Add("x-test-user-id", authenticatedUserId.ToString());
        client.DefaultRequestHeaders.Add(ModuleIdentityMetadata.ModuleSecretHeader, TestModuleSecret);

        var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client,
        });

        return new FilesService.FilesServiceClient(channel);
    }

    /// <summary>
    /// Creates a Files gRPC client that deliberately omits the supervisor-issued module
    /// credential, for asserting that the host rejects anonymous data-plane calls.
    /// </summary>
    /// <param name="authenticatedUserId">The user the request claims to act for.</param>
    public FilesService.FilesServiceClient CreateFilesClientWithoutModuleCredential(Guid authenticatedUserId)
    {
        var client = CreateDefaultClient();
        client.DefaultRequestHeaders.Add("x-test-user-id", authenticatedUserId.ToString());

        var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client,
        });

        return new FilesService.FilesServiceClient(channel);
    }

    public HttpClient CreateAuthenticatedApiClient(Guid authenticatedUserId)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("x-test-user-id", authenticatedUserId.ToString());
        return client;
    }

    /// <summary>
    /// Creates an authenticated <see cref="HttpClient"/> with admin privileges.
    /// </summary>
    public HttpClient CreateAdminApiClient(Guid adminUserId)
    {
        var client = CreateAuthenticatedApiClient(adminUserId);
        client.DefaultRequestHeaders.Add("x-test-user-roles", "Administrator");
        return client;
    }

    private sealed class TestUserStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Headers.TryGetValue("x-test-user-id", out var userHeader) &&
                        Guid.TryParse(userHeader.ToString(), out var userId))
                    {
                        var claims = new List<Claim>
                        {
                            new(ClaimTypes.NameIdentifier, userId.ToString()),
                            new("sub", userId.ToString())
                        };

                        if (context.Request.Headers.TryGetValue("x-test-user-roles", out var rolesHeader))
                        {
                            foreach (var role in rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                claims.Add(new Claim(ClaimTypes.Role, role));
                            }
                        }

                        var identity = new ClaimsIdentity(claims, authenticationType: "IntegrationTest");
                        context.User = new ClaimsPrincipal(identity);
                    }

                    await nextMiddleware();
                });

                next(app);
            };
        }
    }

    /// <summary>
    /// Authentication handler for integration tests that reads x-test-user-id header.
    /// </summary>
    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "IntegrationTest";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.TryGetValue("x-test-user-id", out var userHeader) &&
                Guid.TryParse(userHeader.ToString(), out var userId))
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, userId.ToString()),
                    new("sub", userId.ToString())
                };

                if (Request.Headers.TryGetValue("x-test-user-roles", out var rolesHeader))
                {
                    foreach (var role in rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }
                }

                var identity = new ClaimsIdentity(claims, authenticationType: SchemeName);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            return Task.FromResult(AuthenticateResult.NoResult());
        }
    }
}
