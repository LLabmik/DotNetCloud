using DotNetCloud.Modules.Example.Data;
using DotNetCloud.Modules.Example.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Example.Host.Services;

/// <summary>
/// The notes domain logic behind the module's own web UI.
/// </summary>
/// <remarks>
/// <para>
/// A sandboxed module serves its UI from its own process, so the browser talks to this module's
/// HTTP endpoints rather than to the core. Every operation takes the <b>verified</b> user id — the
/// caller resolves it from the core-signed <c>dnc-user-assertion</c>, never from the request body —
/// and every query is filtered by that id, so one user can never read or delete another user's note.
/// </para>
/// <para>
/// The service is scoped: it wraps the request-scoped <see cref="ExampleDbContext"/>.
/// </para>
/// </remarks>
public sealed class ExampleNotesService
{
    /// <summary>Maximum accepted title length.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Maximum accepted content length.</summary>
    public const int MaxContentLength = 10_000;

    private readonly ExampleDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExampleNotesService"/> class.
    /// </summary>
    /// <param name="db">The module's own database context.</param>
    public ExampleNotesService(ExampleDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>
    /// Lists the notes owned by one user, newest first.
    /// </summary>
    /// <param name="userId">The verified owner.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's notes; never another user's.</returns>
    public async Task<IReadOnlyList<ExampleNoteView>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var notes = await _db.Notes
            .AsNoTracking()
            .Where(note => note.CreatedByUserId == userId)
            .OrderByDescending(note => note.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return notes.Select(ToView).ToArray();
    }

    /// <summary>
    /// Creates a note owned by one user.
    /// </summary>
    /// <param name="userId">The verified owner; never taken from a payload.</param>
    /// <param name="title">The title (trimmed, required, bounded).</param>
    /// <param name="content">The body (optional, bounded).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome and, on success, the stored note.</returns>
    public async Task<ExampleNoteCreateResult> CreateAsync(
        Guid userId,
        string? title,
        string? content,
        CancellationToken cancellationToken = default)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;
        var body = content ?? string.Empty;

        if (trimmedTitle.Length == 0)
        {
            return new ExampleNoteCreateResult(ExampleNoteCreateStatus.TitleRequired, null);
        }

        if (trimmedTitle.Length > MaxTitleLength)
        {
            return new ExampleNoteCreateResult(ExampleNoteCreateStatus.TitleTooLong, null);
        }

        if (body.Length > MaxContentLength)
        {
            return new ExampleNoteCreateResult(ExampleNoteCreateStatus.ContentTooLong, null);
        }

        var note = new ExampleNote
        {
            Title = trimmedTitle,
            Content = body,
            CreatedByUserId = userId,
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExampleNoteCreateResult(ExampleNoteCreateStatus.Created, ToView(note));
    }

    /// <summary>
    /// Deletes one of a user's notes.
    /// </summary>
    /// <param name="userId">The verified owner.</param>
    /// <param name="noteId">The note id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when a note owned by <paramref name="userId"/> was deleted;
    /// <see langword="false"/> when the note does not exist <i>or</i> belongs to somebody else —
    /// the two cases are deliberately indistinguishable so the endpoint is no existence oracle.
    /// </returns>
    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken = default)
    {
        var note = await _db.Notes
            .FirstOrDefaultAsync(
                candidate => candidate.Id == noteId && candidate.CreatedByUserId == userId,
                cancellationToken)
            .ConfigureAwait(false);

        if (note is null)
        {
            return false;
        }

        _db.Notes.Remove(note);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static ExampleNoteView ToView(ExampleNote note) =>
        new(note.Id, note.Title, note.Content, note.CreatedAt, note.UpdatedAt);
}

/// <summary>What <see cref="ExampleNotesService.CreateAsync"/> did.</summary>
public enum ExampleNoteCreateStatus
{
    /// <summary>The note was stored.</summary>
    Created,

    /// <summary>No title (or only whitespace) was supplied.</summary>
    TitleRequired,

    /// <summary>The title exceeds <see cref="ExampleNotesService.MaxTitleLength"/> characters.</summary>
    TitleTooLong,

    /// <summary>The content exceeds <see cref="ExampleNotesService.MaxContentLength"/> characters.</summary>
    ContentTooLong,
}

/// <summary>The result of a create attempt.</summary>
/// <param name="Status">The outcome.</param>
/// <param name="Note">The stored note when <paramref name="Status"/> is
/// <see cref="ExampleNoteCreateStatus.Created"/>; otherwise <see langword="null"/>.</param>
public sealed record ExampleNoteCreateResult(ExampleNoteCreateStatus Status, ExampleNoteView? Note);

/// <summary>A note as the module's UI sees it (no internal entity leakage).</summary>
/// <param name="Id">Note id.</param>
/// <param name="Title">Title.</param>
/// <param name="Content">Body.</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
/// <param name="UpdatedAt">Last update timestamp (UTC), when it was ever updated.</param>
public sealed record ExampleNoteView(
    Guid Id,
    string Title,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
