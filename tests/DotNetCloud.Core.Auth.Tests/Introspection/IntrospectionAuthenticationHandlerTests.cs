using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DotNetCloud.Core.Auth.Introspection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;

namespace DotNetCloud.Core.Auth.Tests.Introspection;

[TestClass]
public sealed class IntrospectionAuthenticationHandlerTests
{
    private Mock<ITokenIntrospectionClient> _clientMock = null!;
    private IMemoryCache _cache = null!;
    private ServiceProvider _services = null!;
    private IOptionsMonitor<IntrospectionAuthenticationOptions> _optionsMonitor = null!;

    [TestInitialize]
    public void Initialize()
    {
        _clientMock = new Mock<ITokenIntrospectionClient>();
        _cache = new MemoryCache(new MemoryCacheOptions { TrackStatistics = true });

        var options = new IntrospectionAuthenticationOptions
        {
            ModuleId = "test-module",
            RequiredAudience = "test-aud",
            CacheDuration = TimeSpan.FromMinutes(1),
        };

        _optionsMonitor = new FixedOptionsMonitor<IntrospectionAuthenticationOptions>(
            "Introspection", options);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_clientMock.Object);
        services.AddSingleton(_cache);
        services.AddSingleton(UrlEncoder.Default);
        _services = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _cache.Dispose();
        _services.Dispose();
    }

    // ============================================
    // Happy path
    // ============================================

    [TestMethod]
    public async Task HandleAuthenticateAsync_ValidToken_ReturnsSuccess()
    {
        // Arrange
        _clientMock.Setup(c => c.IntrospectAsync("valid-token", "test-module", "test-aud", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResult
            {
                Active = true,
                Sub = "user-123",
                Username = "testuser",
                Email = "test@example.com",
                Scopes = new[] { "files:read", "files:write" },
            });

        var handler = CreateHandler("Bearer valid-token");

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Principal);
        Assert.AreEqual("user-123", result.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.AreEqual("testuser", result.Principal.FindFirstValue(ClaimTypes.Name));
        Assert.AreEqual("test@example.com", result.Principal.FindFirstValue(ClaimTypes.Email));
        Assert.IsTrue(result.Principal.HasClaim("scope", "files:read"));
        Assert.IsTrue(result.Principal.HasClaim("scope", "files:write"));
    }

    // ============================================
    // Token rejection
    // ============================================

    [TestMethod]
    public async Task HandleAuthenticateAsync_InactiveToken_ReturnsFail()
    {
        _clientMock.Setup(c => c.IntrospectAsync("bad-token", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResult
            {
                Active = false,
                ErrorDescription = "Token expired.",
            });

        var handler = CreateHandler("Bearer bad-token");
        var result = await handler.AuthenticateAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.IsTrue(result.Failure!.Message.Contains("expired"));
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_EmptyBearerToken_ReturnsFail()
    {
        var handler = CreateHandler("Bearer "); // trailing space, empty token
        var result = await handler.AuthenticateAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.IsTrue(result.Failure!.Message.Contains("empty"));
    }

    // ============================================
    // Pass-through (no Bearer header)
    // ============================================

    [TestMethod]
    public async Task HandleAuthenticateAsync_NoAuthorizationHeader_ReturnsNoResult()
    {
        var handler = CreateHandler(authorizationHeader: null);
        var result = await handler.AuthenticateAsync();

        Assert.IsTrue(result.None);
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_NonBearerScheme_ReturnsNoResult()
    {
        var handler = CreateHandler("Basic dXNlcjpwYXNz");
        var result = await handler.AuthenticateAsync();

        Assert.IsTrue(result.None);
    }

    // ============================================
    // Caching
    // ============================================

    [TestMethod]
    public async Task HandleAuthenticateAsync_CachesSuccessfulResult()
    {
        _clientMock.Setup(c => c.IntrospectAsync("token-x", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResult
            {
                Active = true,
                Sub = "user-1",
            });

        // First call — should hit introspection
        var handler1 = CreateHandler("Bearer token-x");
        var result1 = await handler1.AuthenticateAsync();
        Assert.IsTrue(result1.Succeeded);

        // Second call — should use cache
        var handler2 = CreateHandler("Bearer token-x");
        var result2 = await handler2.AuthenticateAsync();
        Assert.IsTrue(result2.Succeeded);

        // Introspection should have been called exactly once
        _clientMock.Verify(
            c => c.IntrospectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_CachesFailureResult()
    {
        _clientMock.Setup(c => c.IntrospectAsync("bad-token", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResult { Active = false, ErrorDescription = "revoked" });

        // First call
        var handler1 = CreateHandler("Bearer bad-token");
        await handler1.AuthenticateAsync();

        // Second call — should use cached failure
        var handler2 = CreateHandler("Bearer bad-token");
        var result2 = await handler2.AuthenticateAsync();
        Assert.IsFalse(result2.Succeeded);

        _clientMock.Verify(
            c => c.IntrospectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_DifferentTokens_DifferentCacheKeys()
    {
        _clientMock.Setup(c => c.IntrospectAsync("token-a", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResult { Active = true, Sub = "user-a" });
        _clientMock.Setup(c => c.IntrospectAsync("token-b", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResult { Active = true, Sub = "user-b" });

        var handlerA = CreateHandler("Bearer token-a");
        await handlerA.AuthenticateAsync();

        var handlerB = CreateHandler("Bearer token-b");
        await handlerB.AuthenticateAsync();

        // Both tokens should trigger introspection (different cache keys)
        _clientMock.Verify(
            c => c.IntrospectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ============================================
    // Transport errors (NOT cached)
    // ============================================

    [TestMethod]
    public async Task HandleAuthenticateAsync_IntrospectionThrows_ReturnsFail_NotCached()
    {
        _clientMock.Setup(c => c.IntrospectAsync("token-err", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gRPC connection lost"));

        // First attempt — transport error
        var handler1 = CreateHandler("Bearer token-err");
        var result1 = await handler1.AuthenticateAsync();
        Assert.IsFalse(result1.Succeeded);
        Assert.IsTrue(result1.Failure!.Message.Contains("error"));

        // Second attempt — should retry introspection (not cached)
        var handler2 = CreateHandler("Bearer token-err");
        await handler2.AuthenticateAsync();

        _clientMock.Verify(
            c => c.IntrospectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ============================================
    // Challenge / Forbidden responses
    // ============================================

    [TestMethod]
    public async Task HandleChallengeAsync_Sets401AndWWWAuthenticate()
    {
        var context = new DefaultHttpContext { RequestServices = _services };
        var handler = CreateHandler(context, authorizationHeader: null);

        await handler.ChallengeAsync(new AuthenticationProperties());

        Assert.AreEqual(401, context.Response.StatusCode);
        var header = context.Response.Headers.WWWAuthenticate.ToString();
        Assert.IsTrue(header.Contains("invalid_token"));
    }

    [TestMethod]
    public async Task HandleForbiddenAsync_Sets403()
    {
        var context = new DefaultHttpContext { RequestServices = _services };
        var handler = CreateHandler(context, authorizationHeader: null);

        await handler.ForbidAsync(new AuthenticationProperties());

        Assert.AreEqual(403, context.Response.StatusCode);
    }

    // ============================================
    // Module ID / Audience forwarding
    // ============================================

    [TestMethod]
    public async Task HandleAuthenticateAsync_PassesModuleIdAndAudience()
    {
        string? capturedModuleId = null;
        string? capturedAudience = null;

        _clientMock.Setup(c => c.IntrospectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((_, modId, aud, _) =>
            {
                capturedModuleId = modId;
                capturedAudience = aud;
            })
            .ReturnsAsync(new IntrospectionResult { Active = true, Sub = "user-x" });

        var handler = CreateHandler("Bearer any-token");
        await handler.AuthenticateAsync();

        Assert.AreEqual("test-module", capturedModuleId);
        Assert.AreEqual("test-aud", capturedAudience);
    }

    // ============================================
    // Helpers
    // ============================================

    /// Creates a handler wired to a fake HttpContext with the given Authorization header.
    private IntrospectionAuthenticationHandler CreateHandler(string? authorizationHeader)
    {
        var context = new DefaultHttpContext { RequestServices = _services };
        if (authorizationHeader is not null)
            context.Request.Headers.Authorization = new StringValues(authorizationHeader);
        return CreateHandler(context, authorizationHeader);
    }

    private IntrospectionAuthenticationHandler CreateHandler(HttpContext context, string? authorizationHeader)
    {
        if (authorizationHeader is not null)
            context.Request.Headers.Authorization = new StringValues(authorizationHeader);

        var scheme = new AuthenticationScheme(
            "Introspection", "Introspection", typeof(IntrospectionAuthenticationHandler));

        var handler = new IntrospectionAuthenticationHandler(
            _optionsMonitor,
            _services.GetRequiredService<ILoggerFactory>(),
            UrlEncoder.Default,
            _clientMock.Object,
            _cache);

        // Initialize via IAuthenticationHandler interface — the proper way
        // to set up an auth handler in tests.
        ((IAuthenticationHandler)handler).InitializeAsync(scheme, context)
            .GetAwaiter().GetResult();

        return handler;
    }
}

// ============================================
// Test helpers — extend handler for testability
// ============================================

internal static class AuthenticationHandlerTestExtensions
{
    public static Task<AuthenticateResult> AuthenticateAsync<T>(this T handler) where T : AuthenticationHandler<IntrospectionAuthenticationOptions>
    {
        return handler.AuthenticateAsync();
    }

    public static Task ChallengeAsync<T>(this T handler, AuthenticationProperties? properties) where T : AuthenticationHandler<IntrospectionAuthenticationOptions>
    {
        return handler.ChallengeAsync(properties ?? new AuthenticationProperties());
    }

    public static Task ForbidAsync<T>(this T handler, AuthenticationProperties? properties) where T : AuthenticationHandler<IntrospectionAuthenticationOptions>
    {
        return handler.ForbidAsync(properties ?? new AuthenticationProperties());
    }
}

/// <summary>
/// Simple IOptionsMonitor implementation for testing.
/// </summary>
internal sealed class FixedOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
    where TOptions : class, new()
{
    private readonly TOptions _options;

    public FixedOptionsMonitor(string name, TOptions options)
    {
        Name = name;
        _options = options;
    }

    public TOptions CurrentValue => _options;

    public string Name { get; }

    public TOptions Get(string? name) => _options;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null!;
}
