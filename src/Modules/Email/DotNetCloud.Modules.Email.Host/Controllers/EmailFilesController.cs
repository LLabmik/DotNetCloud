using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Security;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Email.Services;
using DotNetCloud.Modules.Sdk.Api;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Email.Host.Controllers;

/// <summary>
/// The bridge between the mailbox and the user's cloud files: browsing the Files tree to attach a
/// file, and saving a received attachment back into it.
/// </summary>
/// <remarks>
/// <para>
/// The module never touches the Files module. Both directions go through the core's module API
/// (<see cref="IDncModuleApi"/>), which holds the capability grant and enforces the user's
/// <c>files:read</c>/<c>files:write</c> consent. That is also why this controller has no notion of
/// node ownership to check: the core resolves the acting user from its own signed assertion and the
/// Files module answers only for that user's nodes.
/// </para>
/// <para>
/// A missing <see cref="IDncModuleApi"/> means the host was not launched by the supervisor (a local
/// <c>dotnet run</c>), so the endpoints answer <c>503 FILES_UNAVAILABLE</c> rather than serving an
/// empty tree that would look like "you have no files".
/// </para>
/// </remarks>
[Route("api/v1/email")]
public class EmailFilesController : EmailControllerBase
{
    private readonly EmailDbContext _db;
    private readonly IAttachmentStorage _attachmentStorage;
    private readonly IFileValidationService _fileValidation;
    private readonly IDncModuleApi? _coreApi;

    /// <summary>Initializes a new instance of the <see cref="EmailFilesController"/> class.</summary>
    /// <param name="db">The module's database context.</param>
    /// <param name="attachmentStorage">Attachment content storage.</param>
    /// <param name="fileValidation">Attachment policy (extension and size limits).</param>
    /// <param name="coreApi">The core module API, or <see langword="null"/> outside the supervisor.</param>
    public EmailFilesController(
        EmailDbContext db,
        IAttachmentStorage attachmentStorage,
        IFileValidationService fileValidation,
        IDncModuleApi? coreApi = null)
    {
        _db = db;
        _attachmentStorage = attachmentStorage;
        _fileValidation = fileValidation;
        _coreApi = coreApi;
    }

    /// <summary>Lists one folder of the acting user's files.</summary>
    /// <param name="parentId">Folder to list; absent lists the root of the user's files.</param>
    /// <returns>The folder's children.</returns>
    [HttpGet("files")]
    public async Task<IActionResult> BrowseFiles([FromQuery] Guid? parentId)
    {
        // Authenticate before anything else: an anonymous probe must be answered 401, never told how
        // this host happens to be deployed.
        GetAuthenticatedCaller();
        var core = RequireCoreApi();

        var nodes = await core.ListFilesAsync(
            parentId?.ToString(),
            page: 1,
            pageSize: 200,
            HttpContext.RequestAborted);

        return Ok(Envelope(new
        {
            nodes = nodes.Select(node => new
            {
                id = node.Id,
                name = node.Name,
                nodeType = node.NodeType,
                mimeType = node.MimeType,
                size = node.Size,
                parentId = node.ParentId,
                updatedAt = node.UpdatedAt,
            }),
            totalCount = nodes.Count,
        }));
    }

    /// <summary>
    /// Copies one of the user's files into the attachment store so it can be sent with a message.
    /// </summary>
    /// <param name="request">The file to attach, as listed by <see cref="BrowseFiles"/>.</param>
    /// <returns>An attachment descriptor in the same shape as <c>upload-attachment</c>.</returns>
    [HttpPost("attachments/from-file")]
    public async Task<IActionResult> AttachFromFiles([FromBody] AttachFromFileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Enforces the assertion gate even though the caller id is not otherwise needed here.
        GetAuthenticatedCaller();
        var core = RequireCoreApi();

        if (!Guid.TryParse(request.NodeId, out _))
        {
            return BadRequest(ErrorEnvelope("invalid_node", "A valid file id is required."));
        }

        var fileName = _fileValidation.SanitizeFileName(request.FileName);
        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType;

        var policy = _fileValidation.Validate(
            fileName,
            request.Size > 0 ? request.Size : 1,
            AllowedFileTypes.EmailAttachmentTypes);

        if (!policy.IsValid)
        {
            return BadRequest(ErrorEnvelope(policy.ErrorCode!, policy.ErrorMessage!));
        }

        await using var content = await core.DownloadFileAsync(request.NodeId, HttpContext.RequestAborted);

        var stored = await _attachmentStorage.StoreAsync(
            content,
            fileName,
            contentType,
            HttpContext.RequestAborted);

        // The declared size is a hint the user's browser supplied; the policy is enforced on what was
        // actually downloaded, so a mislabelled node cannot smuggle an oversized file past it.
        var sizePolicy = _fileValidation.Validate(fileName, stored.Size, AllowedFileTypes.EmailAttachmentTypes);
        if (!sizePolicy.IsValid)
        {
            await _attachmentStorage.DeleteAsync(stored.StorageKey, HttpContext.RequestAborted);

            return BadRequest(ErrorEnvelope(sizePolicy.ErrorCode!, sizePolicy.ErrorMessage!));
        }

        return Ok(Envelope(new
        {
            storageKey = stored.StorageKey,
            fileName,
            contentType,
            size = stored.Size,
            contentHash = stored.ContentHash,
        }));
    }

    /// <summary>Saves a message attachment into the acting user's files.</summary>
    /// <param name="attachmentId">The attachment to save.</param>
    /// <param name="request">The destination folder; absent saves into the user's root.</param>
    /// <returns>The created (or newly versioned) file node.</returns>
    [HttpPost("attachments/{attachmentId:guid}/save-to-files")]
    public async Task<IActionResult> SaveAttachmentToFiles(
        Guid attachmentId,
        [FromBody] SaveToFilesRequest? request)
    {
        var caller = GetAuthenticatedCaller();
        var core = RequireCoreApi();

        var attachment = await _db.EmailAttachments
            .AsNoTracking()
            .Include(a => a.Message)
            .FirstOrDefaultAsync(a => a.Id == attachmentId, HttpContext.RequestAborted)
            ?? throw new NotFoundException("EmailAttachment", attachmentId);

        var ownsAccount = await _db.EmailAccounts
            .AsNoTracking()
            .AnyAsync(
                a => a.Id == attachment.Message!.AccountId && a.OwnerId == caller.UserId,
                HttpContext.RequestAborted);

        if (!ownsAccount)
        {
            throw new ForbiddenException("You do not have access to this attachment.");
        }

        if (string.IsNullOrWhiteSpace(attachment.StorageKey))
        {
            return BadRequest(ErrorEnvelope(
                "attachment_not_stored",
                "This attachment's content is not stored by this module, so it cannot be saved."));
        }

        await using var content = await _attachmentStorage.OpenReadAsync(
            attachment.StorageKey,
            HttpContext.RequestAborted)
            ?? throw new NotFoundException("EmailAttachment", attachmentId);

        var node = await core.UploadFileAsync(
            request?.ParentId,
            attachment.FileName,
            attachment.ContentType,
            content,
            attachment.Size,
            HttpContext.RequestAborted);

        return Ok(Envelope(new
        {
            node = new
            {
                id = node.Id,
                name = node.Name,
                nodeType = node.NodeType,
                mimeType = node.MimeType,
                size = node.Size,
                parentId = node.ParentId,
            },
        }));
    }

    private IDncModuleApi RequireCoreApi() =>
        _coreApi ?? throw new RpcException(new Status(
            Grpc.Core.StatusCode.Unavailable,
            "This module host is not running under the DotNetCloud supervisor, so file access is unavailable."));
}

/// <summary>Request body for attaching one of the user's files to a composed message.</summary>
/// <param name="NodeId">The file node id in the user's Files tree.</param>
/// <param name="FileName">The name shown when the file was picked.</param>
/// <param name="ContentType">The MIME type shown when the file was picked.</param>
/// <param name="Size">The size shown when the file was picked, in bytes.</param>
public sealed record AttachFromFileRequest(
    string NodeId,
    string FileName,
    string? ContentType = null,
    long Size = 0);

/// <summary>Request body for saving an attachment into the user's files.</summary>
/// <param name="ParentId">Destination folder; absent saves into the user's root.</param>
public sealed record SaveToFilesRequest(string? ParentId = null);
