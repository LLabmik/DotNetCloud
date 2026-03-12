using System.Security.Claims;
using System.Text.Encodings.Web;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Initialization;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Files.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

using IDbContextFactory = DotNetCloud.Core.Data.Context.IDbContextFactory;

namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that replaces the real database
/// with an in-memory provider and stubs external dependencies for integration testing.
/// </summary>
internal sealed class DotNetCloudWebApplicationFactory : WebApplicationFactory<DotNetCloud.Core.Server.Program>
{
    private readonly string _databaseName = $"IntTest_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Provide a dummy connection string so the normal startup path doesn't throw.
            // We replace the DbContext below with InMemory, so this is never used.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=integration_test;Username=test;Password=test",
            });
        });

        builder.ConfigureServices(services =>
        {
            // ---------------------------------------------------------------
            // Add test authentication middleware that converts x-test-user-id
            // header to claims for SignalR and HTTP endpoints
            // ---------------------------------------------------------------
            services.AddSingleton<IStartupFilter, TestUserStartupFilter>();

            // ---------------------------------------------------------------
            // Remove Swashbuckle application parts that cause
            // ReflectionTypeLoadException due to Microsoft.OpenApi version
            // mismatch at runtime in the test host.
            // ---------------------------------------------------------------
            var partManager = (ApplicationPartManager?)services
                .FirstOrDefault(d => d.ServiceType == typeof(ApplicationPartManager))
                ?.ImplementationInstance;

            if (partManager is not null)
            {
                var swashbuckleParts = partManager.ApplicationParts
                    .Where(p => p.Name.Contains("Swashbuckle", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var part in swashbuckleParts)
                {
                    partManager.ApplicationParts.Remove(part);
                }
            }

            // ---------------------------------------------------------------
            // Replace CoreDbContext with InMemory provider.
            // We only swap the DbContextOptions so EF Core sees a single
            // provider (InMemory) instead of the original Npgsql.
            // ---------------------------------------------------------------
            services.RemoveAll<DbContextOptions<CoreDbContext>>();
            services.RemoveAll<IDbContextFactory>();

            var namingStrategy = new PostgreSqlNamingStrategy();
            services.AddSingleton<ITableNamingStrategy>(namingStrategy);

            services.AddSingleton<DbContextOptions<CoreDbContext>>(_ =>
            {
                return new DbContextOptionsBuilder<CoreDbContext>()
                    .UseInMemoryDatabase(_databaseName)
                    .Options;
            });

            // Re-register a simple factory that uses InMemory
            services.AddSingleton<IDbContextFactory>(sp =>
            {
                return new InMemoryDbContextFactory(_databaseName, namingStrategy);
            });

            // Replace module DbContexts to avoid external PostgreSQL dependency in tests.
            // Use singleton options (not AddDbContext) to avoid registering a second
            // database provider in the DI container — EF Core rejects dual providers.
            services.RemoveAll<DbContextOptions<FilesDbContext>>();
            services.AddSingleton<DbContextOptions<FilesDbContext>>(_ =>
                new DbContextOptionsBuilder<FilesDbContext>()
                    .UseInMemoryDatabase($"{_databaseName}_files")
                    .Options);

            services.RemoveAll<DbContextOptions<ChatDbContext>>();
            services.AddSingleton<DbContextOptions<ChatDbContext>>(_ =>
                new DbContextOptionsBuilder<ChatDbContext>()
                    .UseInMemoryDatabase($"{_databaseName}_chat")
                    .Options);

            // ---------------------------------------------------------------
            // Stub out ProcessSupervisor so no real child processes spawn
            // ---------------------------------------------------------------
            services.RemoveAll<IProcessSupervisor>();
            var supervisorMock = new Mock<IProcessSupervisor>();
            supervisorMock.Setup(s => s.GetAllModuleInfo()).Returns([]);
            supervisorMock.Setup(s => s.IsModuleRunning(It.IsAny<string>())).Returns(false);
            services.AddSingleton(supervisorMock.Object);

            // Remove only the ProcessSupervisor hosted service, not all hosted services
            var supervisorDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationFactory is not null)
                .ToList();
            foreach (var descriptor in supervisorDescriptors)
            {
                services.Remove(descriptor);
            }

            // ---------------------------------------------------------------
            // Replace DbInitializer to seed test data
            // ---------------------------------------------------------------
            services.RemoveAll<DbInitializer>();
            services.AddScoped<DbInitializer>();

            // ---------------------------------------------------------------
            // Register a test auth scheme and forward Identity.Application
            // to it when x-test-user-id header is present.  This allows
            // SignalR hub [Authorize(AuthenticationSchemes = "Identity.Application,...")]
            // to succeed in integration tests.
            // ---------------------------------------------------------------
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<CookieAuthenticationOptions>(
                IdentityConstants.ApplicationScheme, opts =>
                {
                    opts.ForwardDefaultSelector = context =>
                        context.Request.Headers.ContainsKey("x-test-user-id")
                            ? TestAuthHandler.SchemeName
                            : null;
                });
        });
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> pre-configured to hit the test server.
    /// </summary>
    public HttpClient CreateApiClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }

    /// <summary>
    /// Creates an authenticated <see cref="HttpClient"/> with test user identity.
    /// </summary>
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
    /// Creates an authenticated HTTP client for use with HubConnectionBuilder in in-process SignalR tests.
    /// </summary>
    public HttpClient CreateSignalRClient(Guid authenticatedUserId)
    {
        var handler = Server.CreateHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        client.DefaultRequestHeaders.Add("x-test-user-id", authenticatedUserId.ToString());
        return client;
    }

    /// <summary>
    /// Simple in-memory factory so services that depend on <see cref="IDbContextFactory"/> don't fail.
    /// </summary>
    private sealed class InMemoryDbContextFactory : IDbContextFactory
    {
        private readonly string _dbName;
        private readonly ITableNamingStrategy _naming;

        public InMemoryDbContextFactory(string dbName, ITableNamingStrategy naming)
        {
            _dbName = dbName;
            _naming = naming;
        }

        public DatabaseProvider Provider => DatabaseProvider.PostgreSQL;
        public ITableNamingStrategy NamingStrategy => _naming;

        public CoreDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;
            return new CoreDbContext(options, _naming);
        }
    }

    /// <summary>
    /// Middleware that converts x-test-user-id header to authenticated claims for testing.
    /// Used for standard [Authorize] endpoints (non-scheme-specific).
    /// </summary>
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
                        var identity = new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                            new Claim("sub", userId.ToString())
                        ],
                        authenticationType: "IntegrationTest");

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
    /// Registered as a scheme and forwarded from Identity.Application so scheme-specific
    /// [Authorize] attributes (e.g., on SignalR hubs) succeed in tests.
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
                var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim("sub", userId.ToString())
                ],
                authenticationType: SchemeName);

                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            return Task.FromResult(AuthenticateResult.NoResult());
        }
    }
}
