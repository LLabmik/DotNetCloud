extern alias ChatHost;

using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        });
    }

    /// <summary>
    /// Creates an HTTP client pre-configured for chat API calls.
    /// The Chat module's REST API uses <c>[FromQuery] Guid userId</c>
    /// rather than <c>[Authorize]</c> claims, so no auth headers are needed.
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
