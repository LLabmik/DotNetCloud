using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Consent;

/// <summary>
/// The consent decision endpoints.
/// </summary>
/// <remarks>
/// <para>
/// Security-sensitive by design (F9): the decision is <b>POST-only</b>, validated with the
/// anti-forgery token minted on the prompt, and every response is served with
/// <c>frame-ancestors 'none'</c> plus <c>X-Frame-Options: DENY</c> so the page cannot be embedded in
/// a module iframe or any other site. A GET can only ever render the prompt — it can never approve
/// or deny anything.
/// </para>
/// <para>
/// The POST carries nothing but the prompt id and the answer. The client id, the scopes and the
/// redirect target all come from the server-side <see cref="ConsentRequestStore"/> entry, so a
/// tampered form cannot widen an approval to scopes the user never saw.
/// </para>
/// </remarks>
internal static class ConsentEndpoints
{
    /// <summary>The path of the consent page and its decision handler.</summary>
    internal const string Path = "/connect/consent";

    /// <summary>
    /// Query-string parameter that hands a recorded decision back to the authorize endpoint.
    /// </summary>
    internal const string DecisionParameter = "dnc_consent";

    private const string ContentSecurityPolicy =
        "default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

    /// <summary>Maps the consent endpoints.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapConsentEndpoints(this WebApplication app)
    {
        app.MapGet(Path, (Delegate)HandleGetAsync);
        app.MapPost(Path, (Delegate)HandlePostAsync);

        return app;
    }

    /// <summary>Renders the prompt. Exposed for tests.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The page, or a challenge when the user is not signed in.</returns>
    internal static async Task<IResult> HandleGetAsync(HttpContext context)
    {
        ApplySecurityHeaders(context);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Challenge(
                authenticationSchemes: [IdentityConstants.ApplicationScheme],
                properties: new AuthenticationProperties
                {
                    RedirectUri = context.Request.GetEncodedPathAndQuery(),
                });
        }

        var userId = ResolveUserId(context);
        var store = context.RequestServices.GetRequiredService<ConsentRequestStore>();
        var request = store.Find(context.Request.Query["rid"], userId);

        if (userId == Guid.Empty || request is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Results.Content(
                ConsentPageRenderer.RenderError(
                    "This consent request is unknown, has expired, or was already answered. " +
                    "Return to the application and start again."),
                "text/html; charset=utf-8");
        }

        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(context);

        return Results.Content(
            ConsentPageRenderer.Render(request, tokens.FormFieldName, tokens.RequestToken!, Path),
            "text/html; charset=utf-8");
    }

    /// <summary>Records the user's decision. Exposed for tests.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A redirect back to the authorization endpoint, or an error.</returns>
    internal static async Task<IResult> HandlePostAsync(HttpContext context)
    {
        ApplySecurityHeaders(context);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "invalid_request", error_description = "A form body is required." });
        }

        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            // A missing or stale token is exactly the auto-approve / clickjacking attempt this
            // endpoint must refuse: no consent is written and no decision is recorded.
            return Results.BadRequest(new
            {
                error = "invalid_request",
                error_description = "The anti-forgery token is missing or invalid.",
            });
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var userId = ResolveUserId(context);
        var store = context.RequestServices.GetRequiredService<ConsentRequestStore>();
        var request = store.Find(form["rid"], userId);

        if (request is null)
        {
            return Results.BadRequest(new
            {
                error = "invalid_request",
                error_description = "This consent request is unknown, has expired, or was already answered.",
            });
        }

        var decision = form["decision"].ToString();

        if (decision is not ("allow" or "deny"))
        {
            return Results.BadRequest(new
            {
                error = "invalid_request",
                error_description = "decision must be 'allow' or 'deny'.",
            });
        }

        if (!await RecordDecisionAsync(context, request, userId, decision))
        {
            return Results.BadRequest(new
            {
                error = "invalid_request",
                error_description = "This consent request was already answered.",
            });
        }

        var separator = string.IsNullOrEmpty(request.AuthorizeQueryString)
            || request.AuthorizeQueryString == "?"
                ? "?"
                : "&";

        var authorizeUrl = "/connect/authorize"
            + request.AuthorizeQueryString
            + separator
            + DecisionParameter
            + "="
            + Uri.EscapeDataString(request.RequestId);

        return Results.Redirect(authorizeUrl);
    }

    private static async Task<bool> RecordDecisionAsync(
        HttpContext context,
        PendingConsentRequest request,
        Guid userId,
        string decision)
    {
        var store = context.RequestServices.GetRequiredService<ConsentRequestStore>();
        var consent = context.RequestServices.GetRequiredService<IModuleConsentService>();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DotNetCloud.Consent");

        var approved = decision == "allow";

        // Claim the prompt *before* writing anything. A replay of an already-answered prompt must
        // be refused without side effects — granting first would let a captured form approve a
        // prompt the user had denied.
        if (!store.RecordDecision(request.RequestId, userId, approved ? ConsentDecision.Approved : ConsentDecision.Denied))
        {
            return false;
        }

        if (approved)
        {
            await consent.GrantAsync(
                request.ModuleId,
                userId,
                request.Scopes,
                grantedByUserId: null,
                expiresAtUtc: null,
                context.RequestAborted);
        }

        logger.LogInformation(
            "Consent {Decision} for module {ModuleId} by user {UserId} covering {ScopeCount} scope(s)",
            approved ? "granted" : "denied",
            LogSanitizer.Sanitize(request.ModuleId),
            userId,
            request.Scopes.Count);

        await AuditAsync(context, request, approved);

        return true;
    }

    private static async Task AuditAsync(HttpContext context, PendingConsentRequest request, bool approved)
    {
        try
        {
            var auditLogger = context.RequestServices.GetService<IAuditLogger>();
            if (auditLogger is null)
            {
                return;
            }

            var roles = context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
            var userId = ResolveUserId(context);

            await auditLogger.LogAsync(
                new AuditEntry
                {
                    Caller = new CallerContext(userId, roles, CallerType.User),
                    ModuleId = request.ModuleId,
                    Action = approved ? AuditAction.ModuleConsentGranted : AuditAction.ModuleConsentDenied,
                    EntityType = "ModuleUserConsent",
                    EntityId = userId,
                    Description = approved
                        ? $"User granted {string.Join(' ', request.Scopes)} to {request.ModuleId}."
                        : $"User denied {string.Join(' ', request.Scopes)} to {request.ModuleId}.",
                },
                context.RequestAborted);
        }
        catch (Exception ex)
        {
            // An audit outage must never change the user's decision or block the redirect.
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("DotNetCloud.Consent");
            logger.LogWarning(ex, "Failed to audit a consent decision for module {ModuleId}", request.ModuleId);
        }
    }

    private static Guid ResolveUserId(HttpContext context)
    {
        var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }

    private static void ApplySecurityHeaders(HttpContext context)
    {
        context.Response.Headers["Content-Security-Policy"] = ContentSecurityPolicy;
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
