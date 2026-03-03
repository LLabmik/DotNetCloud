using DotNetCloud.Core.Data;
using DotNetCloud.Core.Data.Context;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Server.Tests;

/// <summary>
/// Factory for creating WebApplicationFactory instances for testing.
/// </summary>
public class DotNetCloudWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Override the configuration to use in-memory database for testing.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the default DbContext configuration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CoreDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<CoreDbContext>((_, options) =>
            {
                options.UseInMemoryDatabase("DotNetCloud_Test");
            });
        });
    }
}

/// <summary>
/// Base class for integration tests.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly HttpClient Client;
    private readonly DotNetCloudWebApplicationFactory _factory;

    protected IntegrationTestBase()
    {
        _factory = new DotNetCloudWebApplicationFactory();
        Client = _factory.CreateClient();
    }

    /// <summary>
    /// Initialize the test (called before each test).
    /// </summary>
    public virtual async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbInitializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
        await dbInitializer.EnsureDatabaseAsync();
    }

    /// <summary>
    /// Dispose the test (called after each test).
    /// </summary>
    public virtual Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get the DbContext for the test.
    /// </summary>
    protected CoreDbContext GetDbContext()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    }
}
