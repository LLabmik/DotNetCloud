using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Grpc.Interceptors;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Tests.Services;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Tests.Quotas;

/// <summary>
/// Tests for <see cref="ModuleRateLimitInterceptor"/>: the RPC budget and the category budgets
/// (events, notifications, audit) are enforced per verified module identity.
/// </summary>
[TestClass]
public class ModuleRateLimitInterceptorTests
{
    private const string ModuleId = "dotnetcloud.rate-limit-test";
    private const string CapabilitiesPrefix = "/dotnetcloud.capabilities.CoreCapabilities/";

    private string _dbName = null!;

    [TestInitialize]
    public void Setup() => _dbName = $"ModuleRateLimitInterceptorTests_{Guid.NewGuid():N}";

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase(_dbName)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
        return services.BuildServiceProvider();
    }

    private void SeedQuota(Action<ModuleQuota> configure)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var row = new ModuleQuota { ModuleId = ModuleId, UpdatedAtUtc = DateTime.UtcNow };
        configure(row);
        db.ModuleQuotas.Add(row);
        db.SaveChanges();
    }

    private ModuleRateLimitInterceptor CreateInterceptor(ServiceProvider provider)
    {
        var resolver = new ModuleQuotaResolver(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ModuleQuotasOptions()),
            Options.Create(new ProcessSupervisorOptions()));

        var cache = new ModuleQuotaCache(resolver, TimeProvider.System);

        return new ModuleRateLimitInterceptor(
            cache,
            new ModuleRateLimiter(TimeProvider.System),
            NullLogger<ModuleRateLimitInterceptor>.Instance);
    }

    private static TestServerCallContext CreateContext(string method, bool withModule = true)
    {
        var context = new TestServerCallContext(method, []);
        if (withModule)
        {
            context.UserState["ModuleId"] = ModuleId;
        }

        return context;
    }

    private static Task<string> Continue(string request, ServerCallContext context) => Task.FromResult("ok");

    [TestMethod]
    public async Task UnaryServerHandler_WhenRequestQuotaExceeded_ThrowsResourceExhausted()
    {
        SeedQuota(row => row.RequestsPerMinute = 2);

        using var provider = CreateProvider();
        var interceptor = CreateInterceptor(provider);

        Assert.AreEqual("ok", await interceptor.UnaryServerHandler(
            "first", CreateContext($"{CapabilitiesPrefix}GetUser"), Continue));
        Assert.AreEqual("ok", await interceptor.UnaryServerHandler(
            "second", CreateContext($"{CapabilitiesPrefix}GetUser"), Continue));

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler(
                "third", CreateContext($"{CapabilitiesPrefix}GetUser"), Continue));

        Assert.AreEqual(StatusCode.ResourceExhausted, ex.StatusCode);
        StringAssert.Contains(ex.Status.Detail, "MODULE_RATE_LIMITED");
        StringAssert.Contains(ex.Status.Detail, "requests");
    }

    [TestMethod]
    public async Task PublishEvent_ConsumesTheEventBudgetSeparately()
    {
        SeedQuota(row =>
        {
            row.RequestsPerMinute = 100;
            row.EventsPerMinute = 1;
        });

        using var provider = CreateProvider();
        var interceptor = CreateInterceptor(provider);

        Assert.AreEqual("ok", await interceptor.UnaryServerHandler(
            "first", CreateContext($"{CapabilitiesPrefix}PublishEvent"), Continue));

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler(
                "second", CreateContext($"{CapabilitiesPrefix}PublishEvent"), Continue));
        Assert.AreEqual(StatusCode.ResourceExhausted, ex.StatusCode);
        StringAssert.Contains(ex.Status.Detail, "event");

        Assert.AreEqual(
            "ok",
            await interceptor.UnaryServerHandler(
                "lookup", CreateContext($"{CapabilitiesPrefix}GetUser"), Continue),
            "The event budget must not block other capability calls");
    }

    [TestMethod]
    public async Task UnaryServerHandler_WithoutModuleIdentity_PassesThrough()
    {
        using var provider = CreateProvider();
        var interceptor = CreateInterceptor(provider);

        for (var i = 0; i < 5; i++)
        {
            Assert.AreEqual(
                "ok",
                await interceptor.UnaryServerHandler(
                    "anonymous", CreateContext($"{CapabilitiesPrefix}GetUser", withModule: false), Continue));
        }
    }
}
