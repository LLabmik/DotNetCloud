extern alias FilesHost;

using System.Security.Claims;
using System.Text.Encodings.Web;
using DotNetCloud.Modules.Files.Data;
using FilesHost::DotNetCloud.Modules.Files.Host;
using FilesHost::DotNetCloud.Modules.Files.Host.Protos;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// Integration test host for the standalone Files module process.
/// </summary>
internal sealed class FilesHostWebApplicationFactory : WebApplicationFactory<FilesHost::Program>
{
    private readonly string _databaseName = $"FilesHostInt_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FilesDbContext>>();
            services.AddDbContext<FilesDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Inject a deterministic test identity from request header for auth-bound gRPC checks.
            services.AddSingleton<IStartupFilter, TestUserStartupFilter>();

            // Register test auth handler for the auth schemes
            // required by FilesControllerBase [Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict...")].
            // The standalone Files.Host process has no OpenIddict or Identity, so we register
            // the test handler directly for both schemes.
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    "Identity.Application", _ => { })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    "OpenIddict.Validation.AspNetCore", _ => { });
        });
    }

    public FilesService.FilesServiceClient CreateFilesClient(Guid authenticatedUserId)
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
