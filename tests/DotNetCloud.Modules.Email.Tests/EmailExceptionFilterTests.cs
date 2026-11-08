using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Email.Host.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Email.Tests;

/// <summary>
/// Tests for <see cref="EmailExceptionFilter"/>: the module authenticates nobody itself, so an
/// unauthenticated call surfaces as an exception and must become a 401 — not a 500 that the UI
/// reports as a module fault.
/// </summary>
[TestClass]
public class EmailExceptionFilterTests
{
    [TestMethod]
    public void OnException_UnauthorizedAccess_ProducesA401Envelope()
    {
        var context = CreateContext(new UnauthorizedAccessException("Authentication is required."));

        CreateFilter().OnException(context);

        Assert.IsTrue(context.ExceptionHandled);
        var result = context.Result as ObjectResult;
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.IsNotNull(result.Value);
    }

    [TestMethod]
    public void OnException_NotFound_ProducesA404Envelope()
    {
        var context = CreateContext(new NotFoundException("Email account not found."));

        CreateFilter().OnException(context);

        Assert.IsTrue(context.ExceptionHandled);
        var result = context.Result as ObjectResult;
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [TestMethod]
    public void OnException_Validation_ProducesA400Envelope()
    {
        var context = CreateContext(new ValidationException("EMAIL_INVALID", "Subject is required."));

        CreateFilter().OnException(context);

        Assert.IsTrue(context.ExceptionHandled);
        var result = context.Result as ObjectResult;
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    [TestMethod]
    public void OnException_UnexpectedException_IsLeftToTheHost()
    {
        var context = CreateContext(new System.InvalidOperationException("boom"));

        CreateFilter().OnException(context);

        // Not swallowed: the host's error pipeline reports it, and the client gets a 500.
        Assert.IsFalse(context.ExceptionHandled);
        Assert.IsNull(context.Result);
    }

    private static EmailExceptionFilter CreateFilter() =>
        new(NullLogger<EmailExceptionFilter>.Instance);

    private static ExceptionContext CreateContext(Exception exception)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ExceptionContext(actionContext, [])
        {
            Exception = exception,
        };
    }
}
