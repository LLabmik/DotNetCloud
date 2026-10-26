using System.Security.Cryptography;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Server.Supervisor;
using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Tests for the module-side adoption of the core-signed end-user assertion.
/// </summary>
/// <remarks>
/// The module must never infer the acting user from a forwarded cookie or token it happens to
/// see; it takes the identity from the core, and rejects a tampered or out-of-scope assertion
/// rather than silently degrading to "anonymous" — degrading would hide an impersonation attempt.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ModuleUserAssertionMiddlewareTests
{
    private const string ModuleId = "dotnetcloud.files";

    private readonly ModuleAssertionKeyRing _keyRing = new();
    private string? _previousPublicKey;
    private string? _previousModuleId;

    [TestInitialize]
    public void Initialize()
    {
        _previousPublicKey = Environment.GetEnvironmentVariable(
            ModuleIdentityMetadata.AssertionPublicKeyEnvironmentVariable);
        _previousModuleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID");

        Environment.SetEnvironmentVariable(
            ModuleIdentityMetadata.AssertionPublicKeyEnvironmentVariable,
            _keyRing.PublicKeyBase64);
        Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULE_ID", ModuleId);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable(
            ModuleIdentityMetadata.AssertionPublicKeyEnvironmentVariable, _previousPublicKey);
        Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULE_ID", _previousModuleId);
        _keyRing.Dispose();
    }

    [TestMethod]
    public async Task InvokeAsync_ValidAssertion_AdoptsAssertionForTheFlow()
    {
        var userId = Guid.CreateVersion7();
        var token = _keyRing.Mint(userId, ModuleId);

        ModuleUserAssertion? observed = null;
        string? ambient = "not-run";

        var middleware = new ModuleUserAssertionMiddleware(_ =>
        {
            observed = ModuleUserAssertionContext.Current is null
                ? null
                : (_keyRing.TryValidate(ModuleUserAssertionContext.Current, ModuleId, out var parsed)
                    ? parsed
                    : null);
            ambient = ModuleUserAssertionContext.Current;
            return Task.CompletedTask;
        });

        var context = CreateContext(token);
        await middleware.InvokeAsync(context);

        Assert.AreEqual(200, context.Response.StatusCode);
        Assert.AreEqual(token, ambient, "The assertion must be visible to downstream work.");
        Assert.IsNotNull(observed);
        Assert.AreEqual(userId, observed.UserId);
        Assert.IsNotNull(context.Items[ModuleIdentityMetadata.UserAssertionHeader]);
    }

    [TestMethod]
    public async Task InvokeAsync_ExpiredAssertion_ReturnsUnauthorized()
    {
        var token = _keyRing.Mint(
            Guid.CreateVersion7(), ModuleId, lifetime: TimeSpan.FromSeconds(-1));

        var reachedNext = false;
        var middleware = new ModuleUserAssertionMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        var context = CreateContext(token);
        await middleware.InvokeAsync(context);

        Assert.AreEqual(401, context.Response.StatusCode);
        Assert.IsFalse(reachedNext);
    }

    [TestMethod]
    public async Task InvokeAsync_AssertionForAnotherModule_ReturnsUnauthorized()
    {
        var token = _keyRing.Mint(Guid.CreateVersion7(), "dotnetcloud.chat");

        var reachedNext = false;
        var middleware = new ModuleUserAssertionMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        var context = CreateContext(token);
        await middleware.InvokeAsync(context);

        Assert.AreEqual(401, context.Response.StatusCode);
        Assert.IsFalse(reachedNext);
    }

    [TestMethod]
    public async Task InvokeAsync_TamperedAssertion_ReturnsUnauthorized()
    {
        var genuine = _keyRing.Mint(Guid.CreateVersion7(), ModuleId);
        var forged = _keyRing.Mint(Guid.CreateVersion7(), ModuleId);
        var tampered = forged[..forged.IndexOf('.', StringComparison.Ordinal)]
            + genuine[genuine.IndexOf('.', StringComparison.Ordinal)..];

        var reachedNext = false;
        var middleware = new ModuleUserAssertionMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        var context = CreateContext(tampered);
        await middleware.InvokeAsync(context);

        Assert.AreEqual(401, context.Response.StatusCode);
        Assert.IsFalse(reachedNext);
    }

    [TestMethod]
    public async Task InvokeAsync_NoAssertion_ContinuesAsModule()
    {
        var reachedNext = false;
        string? ambient = "not-run";

        var middleware = new ModuleUserAssertionMiddleware(_ =>
        {
            reachedNext = true;
            ambient = ModuleUserAssertionContext.Current;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.IsTrue(reachedNext, "Scheduled work and health probes carry no end user.");
        Assert.IsNull(ambient);
        Assert.AreNotEqual(401, context.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_AssertionPresentButNoPublicKey_ReturnsUnauthorized()
    {
        Environment.SetEnvironmentVariable(
            ModuleIdentityMetadata.AssertionPublicKeyEnvironmentVariable, null);

        var token = _keyRing.Mint(Guid.CreateVersion7(), ModuleId);

        var reachedNext = false;
        var middleware = new ModuleUserAssertionMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        var context = CreateContext(token);
        await middleware.InvokeAsync(context);

        Assert.AreEqual(401, context.Response.StatusCode);
        Assert.IsFalse(reachedNext);
    }

    private static DefaultHttpContext CreateContext(string assertion)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ModuleIdentityMetadata.UserAssertionHeader] = assertion;
        return context;
    }
}
