using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Host.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Tests for <see cref="PhotosExceptionFilter"/>: the module authenticates nobody itself, so an
/// unauthenticated call surfaces as an exception and must become a 401 — not a 500 that the UI
/// reports as a module fault.
/// </summary>
[TestClass]
public class PhotosExceptionFilterTests
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
        var context = CreateContext(new NotFoundException("PHOTO_NOT_FOUND", "Photo not found."));

        CreateFilter().OnException(context);

        Assert.IsTrue(context.ExceptionHandled);
        var result = context.Result as ObjectResult;
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [TestMethod]
    public void OnException_Forbidden_ProducesA403Envelope()
    {
        var context = CreateContext(new ForbiddenException("FORBIDDEN", "Not allowed."));

        CreateFilter().OnException(context);

        Assert.IsTrue(context.ExceptionHandled);
        var result = context.Result as ObjectResult;
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status403Forbidden, result.StatusCode);
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

    [TestMethod]
    public void OnException_MissingFileConsent_ProducesAnActionable403()
    {
        // The UI keys its consent prompt off this code; a 500 would leave the user with a dead end.
        var context = CreateContext(new FilesConsentRequiredException("Approve files access."));

        CreateFilter().OnException(context);

        Assert.IsTrue(context.ExceptionHandled);
        var result = context.Result as ObjectResult;
        Assert.IsNotNull(result);
        Assert.AreEqual(StatusCodes.Status403Forbidden, result.StatusCode);

        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        StringAssert.Contains(json, "FILES_CONSENT_REQUIRED");
    }

    private static PhotosExceptionFilter CreateFilter() =>
        new(NullLogger<PhotosExceptionFilter>.Instance);

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
