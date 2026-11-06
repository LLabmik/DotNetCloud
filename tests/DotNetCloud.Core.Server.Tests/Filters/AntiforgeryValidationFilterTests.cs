using Microsoft.AspNetCore.Antiforgery;
using DotNetCloud.Core.Server.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Filters;

/// <summary>
/// Tests for <see cref="AntiforgeryValidationFilter"/> log handling.
/// </summary>
/// <remarks>
/// The request method and path are attacker-controlled. They must not reach the log with embedded
/// CR/LF characters, or a forged request could inject fake log entries.
/// </remarks>
[TestClass]
public sealed class AntiforgeryValidationFilterTests
{
    [TestMethod]
    public async Task OnAuthorizationAsync_InvalidTokenWithCrlfInRequestValues_WritesSanitizedLogEntry()
    {
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery
            .Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .ThrowsAsync(new AntiforgeryValidationException("invalid token"));
        var logger = new CapturingLogger<AntiforgeryValidationFilter>();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST\r\nFORGED ENTRY";
        httpContext.Request.Path = "/admin\r\nFORGED ENTRY";
        var filterContext = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            []);

        var filter = new AntiforgeryValidationFilter(antiforgery.Object, logger);
        await filter.OnAuthorizationAsync(filterContext);

        Assert.IsInstanceOfType<BadRequestObjectResult>(filterContext.Result);
        var message = AssertSingleMessage(logger);
        Assert.IsFalse(message.Contains('\r'), "Log entry must not contain a carriage return.");
        Assert.IsFalse(message.Contains('\n'), "Log entry must not contain a line feed.");
    }

    private static string AssertSingleMessage<T>(CapturingLogger<T> logger)
    {
        Assert.AreEqual(1, logger.Messages.Count);
        return logger.Messages[0];
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
