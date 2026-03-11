extern alias FilesHost;

using System.Security.Claims;
using DotNetCloud.Modules.Files.Data;
using FilesHost::DotNetCloud.Modules.Files.Host;
using FilesHost::DotNetCloud.Modules.Files.Host.Protos;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
}
