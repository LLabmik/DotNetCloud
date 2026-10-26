using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Filters;

/// <summary>
/// Requires a valid antiforgery token on an action.
/// </summary>
/// <remarks>
/// The framework's <c>ValidateAntiForgeryTokenAttribute</c> resolves a filter from
/// <c>Microsoft.AspNetCore.Mvc.ViewFeatures</c>, which a controllers-only application does not
/// register — using it here produced a 500 on every form post. This attribute registers a filter
/// that depends only on <see cref="IAntiforgery"/>, which the application already provides.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireAntiforgeryTokenAttribute : TypeFilterAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequireAntiforgeryTokenAttribute"/> class.
    /// </summary>
    public RequireAntiforgeryTokenAttribute()
        : base(typeof(AntiforgeryValidationFilter))
    {
    }
}

/// <summary>
/// Rejects a request whose antiforgery token is missing or does not match the caller's cookie.
/// </summary>
public sealed class AntiforgeryValidationFilter : IAsyncAuthorizationFilter
{
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<AntiforgeryValidationFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AntiforgeryValidationFilter"/> class.
    /// </summary>
    /// <param name="antiforgery">The antiforgery service.</param>
    /// <param name="logger">Logger.</param>
    public AntiforgeryValidationFilter(
        IAntiforgery antiforgery,
        ILogger<AntiforgeryValidationFilter> logger)
    {
        _antiforgery = antiforgery;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            _logger.LogWarning(
                ex,
                "Antiforgery validation failed for {Method} {Path}",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path);

            context.Result = new BadRequestObjectResult(new
            {
                code = "ANTIFORGERY_VALIDATION_FAILED",
                message = "The request could not be verified. Reload the page and try again.",
            });
        }
    }
}
