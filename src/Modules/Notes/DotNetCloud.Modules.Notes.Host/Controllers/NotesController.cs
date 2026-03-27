using System.ComponentModel.DataAnnotations;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Notes.Models;
using DotNetCloud.Modules.Notes.Services;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using ValidationException = DotNetCloud.Core.Errors.ValidationException;

namespace DotNetCloud.Modules.Notes.Host.Controllers;

/// <summary>
/// REST API controller for note CRUD, search, folders, version history, and sharing.
/// </summary>
[Route("api/v1/notes")]
public class NotesController : NotesControllerBase
{
    private readonly INoteService _noteService;
    private readonly INoteFolderService _folderService;
    private readonly INoteShareService _shareService;
    private readonly IMarkdownRenderer _markdownRenderer;
    private readonly ILogger<NotesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotesController"/> class.
    /// </summary>
    public NotesController(
        INoteService noteService,
        INoteFolderService folderService,
        INoteShareService shareService,
        IMarkdownRenderer markdownRenderer,
        ILogger<NotesController> logger)
    {
        _noteService = noteService;
        _folderService = folderService;
        _shareService = shareService;
        _markdownRenderer = markdownRenderer;
        _logger = logger;
    }

    // ─── Note CRUD ────────────────────────────────────────────────────────

    /// <summary>Lists notes for the authenticated user with optional folder filter.</summary>
    [HttpGet]
    public async Task<IActionResult> ListNotesAsync(
        [FromQuery] Guid? folderId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var notes = await _noteService.ListNotesAsync(caller, folderId, skip, take);
        return Ok(Envelope(notes));
    }

    /// <summary>Gets a note by ID.</summary>
    [HttpGet("{noteId:guid}")]
    public async Task<IActionResult> GetNoteAsync(Guid noteId)
    {
        var caller = GetAuthenticatedCaller();
        var note = await _noteService.GetNoteAsync(noteId, caller);
        return note is null
            ? NotFound(ErrorEnvelope(ErrorCodes.NoteNotFound, "Note not found."))
            : Ok(Envelope(note));
    }

    /// <summary>Creates a new note.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateNoteAsync([FromBody] CreateNoteDto dto)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var note = await _noteService.CreateNoteAsync(dto, caller);
            return Created($"/api/v1/notes/{note.Id}", Envelope(note));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates an existing note.</summary>
    [HttpPut("{noteId:guid}")]
    public async Task<IActionResult> UpdateNoteAsync(Guid noteId, [FromBody] UpdateNoteDto dto)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var note = await _noteService.UpdateNoteAsync(noteId, dto, caller);
            return Ok(Envelope(note));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode switch
            {
                ErrorCodes.NoteNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                ErrorCodes.NoteVersionConflict => Conflict(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
    }

    /// <summary>Soft-deletes a note.</summary>
    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> DeleteNoteAsync(Guid noteId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            await _noteService.DeleteNoteAsync(noteId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Search ───────────────────────────────────────────────────────────

    /// <summary>Searches notes by title, content, and tags.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchNotesAsync(
        [FromQuery] string? q = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var notes = await _noteService.SearchNotesAsync(caller, q, skip, take);
        return Ok(Envelope(notes));
    }

    // ─── Version History ──────────────────────────────────────────────────

    /// <summary>Gets version history for a note.</summary>
    [HttpGet("{noteId:guid}/versions")]
    public async Task<IActionResult> GetVersionHistoryAsync(Guid noteId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var versions = await _noteService.GetVersionHistoryAsync(noteId, caller);
            return Ok(Envelope(versions));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Restores a note to a previous version.</summary>
    [HttpPost("{noteId:guid}/versions/{versionId:guid}/restore")]
    public async Task<IActionResult> RestoreVersionAsync(Guid noteId, Guid versionId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var note = await _noteService.RestoreVersionAsync(noteId, versionId, caller);
            return Ok(Envelope(note));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode == ErrorCodes.NoteNotFound || ex.ErrorCode == ErrorCodes.NoteVersionNotFound
                ? NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Folders ──────────────────────────────────────────────────────────

    /// <summary>Lists folders for the authenticated user.</summary>
    [HttpGet("folders")]
    public async Task<IActionResult> ListFoldersAsync([FromQuery] Guid? parentId = null)
    {
        var caller = GetAuthenticatedCaller();
        var folders = await _folderService.ListFoldersAsync(caller, parentId);
        return Ok(Envelope(folders));
    }

    /// <summary>Gets a folder by ID.</summary>
    [HttpGet("folders/{folderId:guid}")]
    public async Task<IActionResult> GetFolderAsync(Guid folderId)
    {
        var caller = GetAuthenticatedCaller();
        var folder = await _folderService.GetFolderAsync(folderId, caller);
        return folder is null
            ? NotFound(ErrorEnvelope(ErrorCodes.NoteFolderNotFound, "Folder not found."))
            : Ok(Envelope(folder));
    }

    /// <summary>Creates a new note folder.</summary>
    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolderAsync([FromBody] CreateNoteFolderDto dto)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var folder = await _folderService.CreateFolderAsync(dto, caller);
            return Created($"/api/v1/notes/folders/{folder.Id}", Envelope(folder));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates an existing folder.</summary>
    [HttpPut("folders/{folderId:guid}")]
    public async Task<IActionResult> UpdateFolderAsync(Guid folderId, [FromBody] UpdateNoteFolderDto dto)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var folder = await _folderService.UpdateFolderAsync(folderId, dto, caller);
            return Ok(Envelope(folder));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode == ErrorCodes.NoteFolderNotFound
                ? NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Soft-deletes a folder (notes become un-filed).</summary>
    [HttpDelete("folders/{folderId:guid}")]
    public async Task<IActionResult> DeleteFolderAsync(Guid folderId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            await _folderService.DeleteFolderAsync(folderId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Sharing ──────────────────────────────────────────────────────────

    /// <summary>Lists shares for a note.</summary>
    [HttpGet("{noteId:guid}/shares")]
    public async Task<IActionResult> ListSharesAsync(Guid noteId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var shares = await _shareService.ListSharesAsync(noteId, caller);
            return Ok(Envelope(shares));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Shares a note with a user.</summary>
    [HttpPost("{noteId:guid}/shares")]
    public async Task<IActionResult> ShareNoteAsync(Guid noteId, [FromBody] ShareNoteRequest request)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var share = await _shareService.ShareNoteAsync(
                noteId, request.UserId, request.Permission, caller);
            return Created($"/api/v1/notes/{noteId}/shares", Envelope(share));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode == ErrorCodes.NoteNotFound
                ? NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Removes a note share.</summary>
    [HttpDelete("shares/{shareId:guid}")]
    public async Task<IActionResult> RemoveShareAsync(Guid shareId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            await _shareService.RemoveShareAsync(shareId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Markdown Rendering ───────────────────────────────────────────────

    /// <summary>Renders a saved note's Markdown content to sanitized HTML.</summary>
    [HttpGet("{noteId:guid}/preview")]
    public async Task<IActionResult> PreviewNoteAsync(Guid noteId)
    {
        var caller = GetAuthenticatedCaller();
        var note = await _noteService.GetNoteAsync(noteId, caller);

        if (note is null)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NoteNotFound, "Note not found."));
        }

        var html = note.Format == NoteContentFormat.Markdown
            ? _markdownRenderer.RenderToHtml(note.Content)
            : _markdownRenderer.SanitizeHtml(note.Content);

        return Ok(Envelope(new NotePreviewResponse
        {
            NoteId = note.Id,
            Title = note.Title,
            RenderedHtml = html,
            Format = note.Format,
            Version = note.Version,
        }));
    }

    /// <summary>Renders raw Markdown to sanitized HTML for live preview without saving.</summary>
    [HttpPost("render")]
    public IActionResult RenderMarkdownAsync([FromBody] RenderMarkdownRequest request)
    {
        if (string.IsNullOrEmpty(request.Content))
        {
            return Ok(Envelope(new { html = string.Empty }));
        }

        var html = request.Format == NoteContentFormat.Markdown
            ? _markdownRenderer.RenderToHtml(request.Content)
            : _markdownRenderer.SanitizeHtml(request.Content);

        return Ok(Envelope(new { html }));
    }
}

/// <summary>Request body for sharing a note.</summary>
public sealed record ShareNoteRequest
{
    /// <summary>User ID to share with.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Permission level to grant.</summary>
    public NoteSharePermission Permission { get; init; }
}

/// <summary>Response for a rendered note preview.</summary>
public sealed record NotePreviewResponse
{
    /// <summary>The note ID.</summary>
    public required Guid NoteId { get; init; }

    /// <summary>Note title.</summary>
    public required string Title { get; init; }

    /// <summary>Sanitized HTML output.</summary>
    public required string RenderedHtml { get; init; }

    /// <summary>Original content format.</summary>
    public NoteContentFormat Format { get; init; }

    /// <summary>Note version at render time.</summary>
    public int Version { get; init; }
}

/// <summary>Request body for rendering Markdown to HTML.</summary>
public sealed record RenderMarkdownRequest
{
    /// <summary>Raw content to render.</summary>
    [Required]
    public required string Content { get; init; }

    /// <summary>Content format (defaults to Markdown).</summary>
    public NoteContentFormat Format { get; init; } = NoteContentFormat.Markdown;
}
