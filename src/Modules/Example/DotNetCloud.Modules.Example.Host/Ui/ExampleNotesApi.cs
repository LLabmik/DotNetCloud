using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Modules.Ui;
using DotNetCloud.Modules.Example.Host.Services;
using DotNetCloud.Modules.Sdk.Api;

namespace DotNetCloud.Modules.Example.Host.Ui;

/// <summary>
/// The JSON API behind the Example module's own web UI.
/// </summary>
/// <remarks>
/// <para>
/// These endpoints exist because the module owns its UI (phase A5): the core frames
/// <c>/apps/dotnetcloud.example/</c>, proxies it to this process, and the browser then calls the same
/// proxied origin for data. The module is therefore the only place that may answer.
/// </para>
/// <para>
/// Authentication is the core's, not the module's: the proxy strips the user's session cookie and
/// mints a short-lived <c>dnc-user-assertion</c> bound to this module, which
/// <see cref="ModuleUserAssertionMiddleware"/> verifies on the way in. The acting user comes from
/// <see cref="IModuleUserAssertionAccessor"/> — never from the request — so a caller cannot name
/// somebody else, and a request without an assertion is rejected rather than treated as anonymous.
/// </para>
/// </remarks>
internal static class ExampleNotesApi
{
    /// <summary>The module's own id, used for shell links in notifications.</summary>
    private const string ModuleId = "dotnetcloud.example";

    private const string AuditEntity = "ExampleNote";

    private const string AuditCreateAction = "create-note";

    private const string AuditDeleteAction = "delete-note";

    /// <summary>
    /// Maps the module's note endpoints.
    /// </summary>
    /// <param name="app">The module host application.</param>
    internal static void MapExampleNotesApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Per-user data: a shared or intermediary cache must never store these responses.
        app.MapGet("/api/notes", ListAsync).WithNoStore();
        app.MapPost("/api/notes", CreateAsync).WithNoStore();
        app.MapDelete("/api/notes/{noteId:guid}", DeleteAsync).WithNoStore();

        // The module UI is a sandboxed frame with no cookie of its own, so it asks its host who it is
        // acting as. Only the id is available: this module declares no IUserDirectory capability.
        app.MapGet("/api/v1/me", (IModuleUserAssertionAccessor userAssertion) =>
            ResolveUserId(userAssertion) is { } userId
                ? Results.Ok(new { userId, displayName = (string?)null, email = (string?)null })
                : Results.Unauthorized()).WithNoStore();
    }

    internal static async Task<IResult> ListAsync(
        ExampleNotesService notes,
        IModuleUserAssertionAccessor userAssertion,
        CancellationToken cancellationToken)
    {
        if (ResolveUserId(userAssertion) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var userNotes = await notes.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { notes = userNotes });
    }

    internal static async Task<IResult> CreateAsync(
        ExampleNoteRequest? request,
        ExampleNotesService notes,
        IModuleUserAssertionAccessor userAssertion,
        IDncModuleApi coreApi,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (ResolveUserId(userAssertion) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await notes
            .CreateAsync(userId, request?.Title, request?.Content, cancellationToken)
            .ConfigureAwait(false);

        if (result.Status is not ExampleNoteCreateStatus.Created || result.Note is null)
        {
            return Results.BadRequest(new { error = Describe(result.Status) });
        }

        // The note is already stored: an audit failure must not turn a successful write into an
        // error the user retries, so it is logged (never with the user id or note text) and swallowed.
        try
        {
            await coreApi
                .LogAuditAsync(AuditAction.Create, AuditEntity, result.Note.Id, AuditCreateAction, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger("ExampleNotesApi")
                .LogWarning(ex, "Audit write failed for a note created through the module UI.");
        }

        // Demonstrates the other declared capability and the consent plane: the core delivers this
        // only when the user consented to 'notifications:send', and filters the recipient otherwise.
        // The notification never repeats the note text (it is the user's own content, not ours to copy).
        try
        {
            await coreApi
                .SendNotificationAsync(
                    new DncNotification(
                        Title: "Note created",
                        Body: "Your example note was saved.",
                        Category: "Example",
                        Link: ModuleAppUrls.ShellPath(ModuleId),
                        RecipientUserIds: [userId.ToString()]),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger("ExampleNotesApi")
                .LogWarning(ex, "Notification delivery failed for a note created through the module UI.");
        }

        return Results.Created($"/api/notes/{result.Note.Id}", result.Note);
    }

    internal static async Task<IResult> DeleteAsync(
        Guid noteId,
        ExampleNotesService notes,
        IModuleUserAssertionAccessor userAssertion,
        IDncModuleApi coreApi,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (ResolveUserId(userAssertion) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var deleted = await notes.DeleteAsync(userId, noteId, cancellationToken).ConfigureAwait(false);

        if (!deleted)
        {
            // Unknown id and another user's id answer identically on purpose.
            return Results.NotFound();
        }

        try
        {
            await coreApi
                .LogAuditAsync(AuditAction.Delete, AuditEntity, noteId, AuditDeleteAction, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger("ExampleNotesApi")
                .LogWarning(ex, "Audit write failed for a note deleted through the module UI.");
        }

        return Results.NoContent();
    }

    /// <summary>
    /// Gets the verified acting user, or <see langword="null"/> when the request carries no usable
    /// assertion (the proxy always mints one for a signed-in user, so this means "not signed in").
    /// </summary>
    private static Guid? ResolveUserId(IModuleUserAssertionAccessor userAssertion)
    {
        var userId = userAssertion.GetCurrentUserId();
        return userId is { } value && value != Guid.Empty ? value : null;
    }

    private static string Describe(ExampleNoteCreateStatus status) => status switch
    {
        ExampleNoteCreateStatus.TitleRequired => "A title is required.",
        ExampleNoteCreateStatus.TitleTooLong =>
            $"The title must be at most {ExampleNotesService.MaxTitleLength} characters.",
        ExampleNoteCreateStatus.ContentTooLong =>
            $"The content must be at most {ExampleNotesService.MaxContentLength} characters.",
        _ => "The note could not be created.",
    };

    /// <summary>Marks a response as per-user and uncacheable.</summary>
    private static RouteHandlerBuilder WithNoStore(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(static async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context).ConfigureAwait(false);
        });
}

/// <summary>
/// The body of a create-note request from the module UI.
/// </summary>
/// <param name="Title">The note title.</param>
/// <param name="Content">The note body.</param>
/// <remarks>
/// There is deliberately no user id here: the owner is the verified caller, and accepting one from
/// the payload would be a spoofing path.
/// </remarks>
public sealed record ExampleNoteRequest(string? Title, string? Content);
