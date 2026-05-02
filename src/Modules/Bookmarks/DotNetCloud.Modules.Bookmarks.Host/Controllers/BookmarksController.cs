using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Bookmarks.Services;
using DotNetCloud.Modules.Search.Client;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Bookmarks.Host.Controllers;

/// <summary>
/// REST API controller for Bookmarks module endpoints.
/// </summary>
[Route("api/v1/bookmarks")]
public class BookmarksController : BookmarksControllerBase
{
    private readonly IBookmarkService _bookmarkService;
    private readonly IBookmarkFolderService _folderService;
    private readonly IBookmarkImportExportService _importExportService;
    private readonly IBookmarkPreviewService _previewService;
    private readonly ISearchFtsClient? _searchFtsClient;
    private readonly ILogger<BookmarksController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarksController"/> class.
    /// </summary>
    public BookmarksController(
        IBookmarkService bookmarkService,
        IBookmarkFolderService folderService,
        IBookmarkImportExportService importExportService,
        IBookmarkPreviewService previewService,
        ISearchFtsClient? searchFtsClient,
        ILogger<BookmarksController> logger)
    {
        _bookmarkService = bookmarkService;
        _folderService = folderService;
        _importExportService = importExportService;
        _previewService = previewService;
        _searchFtsClient = searchFtsClient;
        _logger = logger;
    }

    // ── Bookmarks ──────────────────────────────────────────

    /// <summary>Lists bookmarks for the authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? folderId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var bookmarks = await _bookmarkService.ListAsync(caller, folderId, skip, take);
            return Ok(Envelope(bookmarks));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets a bookmark by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var bookmark = await _bookmarkService.GetAsync(id, caller);
            if (bookmark is null) return NotFound(ErrorEnvelope(ErrorCodes.BookmarkNotFound, "Bookmark not found."));
            return Ok(Envelope(bookmark));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Creates a new bookmark.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookmarkRequest request)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var bookmark = await _bookmarkService.CreateAsync(request, caller);
            return CreatedAtAction(nameof(Get), new { id = bookmark.Id }, Envelope(bookmark));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates an existing bookmark.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBookmarkRequest request)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var bookmark = await _bookmarkService.UpdateAsync(id, request, caller);
            return Ok(Envelope(bookmark));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode switch
            {
                ErrorCodes.BookmarkNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
    }

    /// <summary>Deletes a bookmark (soft-delete).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            await _bookmarkService.DeleteAsync(id, caller);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode switch
            {
                ErrorCodes.BookmarkNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
    }

    /// <summary>Searches bookmarks.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q = "", [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var results = await _bookmarkService.SearchAsync(caller, q, skip, take);
            return Ok(Envelope(results));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ── Folders ────────────────────────────────────────────

    /// <summary>Lists bookmark folders.</summary>
    [HttpGet("folders")]
    public async Task<IActionResult> ListFolders([FromQuery] Guid? parentId)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var folders = await _folderService.ListAsync(caller, parentId);
            return Ok(Envelope(folders));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets a folder by ID.</summary>
    [HttpGet("folders/{id:guid}")]
    public async Task<IActionResult> GetFolder(Guid id)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var folder = await _folderService.GetAsync(id, caller);
            if (folder is null) return NotFound(ErrorEnvelope(ErrorCodes.BookmarkFolderNotFound, "Folder not found."));
            return Ok(Envelope(folder));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Creates a new folder.</summary>
    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder([FromBody] CreateBookmarkFolderRequest request)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var folder = await _folderService.CreateAsync(request, caller);
            return CreatedAtAction(nameof(GetFolder), new { id = folder.Id }, Envelope(folder));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates a folder.</summary>
    [HttpPut("folders/{id:guid}")]
    public async Task<IActionResult> UpdateFolder(Guid id, [FromBody] UpdateBookmarkFolderRequest request)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var folder = await _folderService.UpdateAsync(id, request, caller);
            return Ok(Envelope(folder));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode switch
            {
                ErrorCodes.BookmarkFolderNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
    }

    /// <summary>Deletes a folder.</summary>
    [HttpDelete("folders/{id:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid id)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            await _folderService.DeleteAsync(id, caller);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode switch
            {
                ErrorCodes.BookmarkFolderNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
    }

    // ── Import/Export ──────────────────────────────────────

    /// <summary>Imports bookmarks from a browser HTML export file.</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromForm] IFormFile? file, [FromQuery] Guid? folderId)
    {
        try
        {
            if (file is null || file.Length == 0)
                return BadRequest(ErrorEnvelope(ErrorCodes.BookmarkImportFailed, "No file provided."));

            var caller = GetAuthenticatedCaller();
            using var stream = file.OpenReadStream();
            var result = await _importExportService.ImportHtmlAsync(stream, folderId, caller);
            return Ok(Envelope(result));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Exports bookmarks to browser HTML format.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] Guid? folderId)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var stream = await _importExportService.ExportHtmlAsync(caller, folderId);
            return File(stream, "text/html", "bookmarks.html");
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ── Previews ───────────────────────────────────────────

    /// <summary>Triggers a preview fetch for a bookmark.</summary>
    [HttpPost("{id:guid}/preview/fetch")]
    public async Task<IActionResult> FetchPreview(Guid id)
    {
        try
        {
            var preview = await _previewService.FetchPreviewAsync(id);
            return Ok(Envelope(preview));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets the current preview for a bookmark.</summary>
    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> GetPreview(Guid id)
    {
        try
        {
            var preview = await _previewService.GetPreviewAsync(id);
            if (preview is null) return NotFound(ErrorEnvelope(ErrorCodes.BookmarkNotFound, "Preview not found."));
            return Ok(Envelope(preview));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}
