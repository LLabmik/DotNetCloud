using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Initialization;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Files.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
            services.RemoveAll<DbContextOptions<FilesDbContext>>();
            services.RemoveAll<DbContextOptions<ChatDbContext>>();
            services.AddDbContext<FilesDbContext>(options =>
                options.UseInMemoryDatabase($"{_databaseName}_files"));
            services.AddDbContext<ChatDbContext>(options =>
                options.UseInMemoryDatabase($"{_databaseName}_chat"));

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
}
