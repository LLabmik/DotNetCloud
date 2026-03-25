extern alias ChatHost;

using System.Security.Claims;
using System.Text.Encodings.Web;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.Data;
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
/// Integration test host for the standalone Chat module process.
/// </summary>
internal sealed class ChatHostWebApplicationFactory : WebApplicationFactory<ChatHost::Program>
{
    private readonly string _databaseName = $"ChatHostInt_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace ChatDbContext with an in-memory database scoped to this test run.
            services.RemoveAll<DbContextOptions<ChatDbContext>>();
            services.AddDbContext<ChatDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Provide a no-op IRealtimeBroadcaster since the chat host runs
            // independently and doesn't own Signal R hub infrastructure.
            services.RemoveAll<IRealtimeBroadcaster>();
            services.AddSingleton<IRealtimeBroadcaster, NoOpRealtimeBroadcaster>();

            // Inject a deterministic test identity from request header for auth-bound checks.
            services.AddSingleton<IStartupFilter, TestUserStartupFilter>();

            // Register test auth handler for the auth schemes
            // required by ChatControllerBase [Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict...")].
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    "Identity.Application", _ => { })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    "OpenIddict.Validation.AspNetCore", _ => { });
        });
    }

    /// <summary>
    /// Creates an HTTP client pre-configured for authenticated chat API calls.
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

    /// <summary>
    /// No-op broadcaster for isolated REST API integration tests that do not
    /// exercise an actual SignalR hub.
    /// </summary>
    private sealed class NoOpRealtimeBroadcaster : IRealtimeBroadcaster
    {
        public Task BroadcastAsync(string group, string eventName, object message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendToUserAsync(Guid userId, string eventName, object message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendToRoleAsync(string role, string eventName, object message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddToGroupAsync(Guid userId, string group, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(Guid userId, string group, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
