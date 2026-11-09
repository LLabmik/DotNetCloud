using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.AI.Host.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Tests for <see cref="AiRequireUserAssertionFilter"/>: the sandboxed module authenticates nobody
/// itself, so every REST action — including the provider health probe and the settings read-out —
/// must refuse a call that carries no verified end-user assertion.
/// </summary>
[TestClass]
public class AiRequireUserAssertionFilterTests
{
    [TestMethod]
    public void OnAuthorization_WithoutAssertion_ProducesA401()
    {
        var context = CreateContext(assertedUserId: null);

        new AiRequireUserAssertionFilter().OnAuthorization(context);

        var result = context.Result as ObjectResult;
        Assert.IsNotNull(result, "a call without an assertion must be refused");
        Assert.AreEqual(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [TestMethod]
    public void OnAuthorization_WithAssertion_AllowsTheAction()
    {
        var context = CreateContext(Guid.CreateVersion7());

        new AiRequireUserAssertionFilter().OnAuthorization(context);

        Assert.IsNull(context.Result, "an asserted user must pass through to the action");
    }

    [TestMethod]
    public void OnAuthorization_WithEmptyAssertedUser_ProducesA401()
    {
        var context = CreateContext(Guid.Empty);

        new AiRequireUserAssertionFilter().OnAuthorization(context);

        Assert.IsNotNull(context.Result);
    }

    [TestMethod]
    public void OnAuthorization_WithoutTheAccessorRegistered_FailsClosed()
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var context = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            []);

        new AiRequireUserAssertionFilter().OnAuthorization(context);

        Assert.IsNotNull(context.Result, "a host without the accessor must not open the API");
    }

    private static AuthorizationFilterContext CreateContext(Guid? assertedUserId)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IModuleUserAssertionAccessor>(new StubAccessor(assertedUserId));

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            []);
    }

    private sealed class StubAccessor(Guid? userId) : IModuleUserAssertionAccessor
    {
        public Guid? GetCurrentUserId() => userId;
    }
}
