using System.Globalization;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.Host.Protos;
using DotNetCloud.Modules.Notes.Services;
using Grpc.Core;

namespace DotNetCloud.Modules.Notes.Host.Services;

/// <summary>
/// gRPC service implementation for the Notes module.
/// Exposes note and folder operations over gRPC for the core server to invoke.
/// </summary>
public sealed class NotesGrpcService : Protos.NotesGrpcService.NotesGrpcServiceBase
{
    private readonly INoteService _noteService;
    private readonly INoteFolderService _folderService;
    private readonly ILogger<NotesGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotesGrpcService"/> class.
    /// </summary>
    public NotesGrpcService(
        INoteService noteService,
        INoteFolderService folderService,
        ILogger<NotesGrpcService> logger)
    {
        _noteService = noteService;
        _folderService = folderService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<NoteResponse> CreateNote(
        CreateNoteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new NoteResponse { Success = false, ErrorMessage = "Invalid user ID format." };

        var format = Enum.TryParse<NoteContentFormat>(request.Format, true, out var f)
            ? f : NoteContentFormat.Markdown;

        var dto = new CreateNoteDto
        {
            FolderId = Guid.TryParse(request.FolderId, out var fid) ? fid : null,
            Title = request.Title,
            Content = request.Content ?? string.Empty,
            Format = format,
            Tags = request.Tags.ToList(),
            Links = request.Links.Select(ToLinkDto).ToList()
        };

        try
        {
            var result = await _noteService.CreateNoteAsync(
                dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new NoteResponse { Success = true, Note = ToNoteMessage(result) };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new NoteResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<NoteResponse> GetNote(
        GetNoteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NoteId, out var noteId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new NoteResponse { Success = false, ErrorMessage = "Invalid ID format." };

        var result = await _noteService.GetNoteAsync(
            noteId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

        return result is null
            ? new NoteResponse { Success = false, ErrorMessage = "Note not found." }
            : new NoteResponse { Success = true, Note = ToNoteMessage(result) };
    }

    /// <inheritdoc />
    public override async Task<ListNotesResponse> ListNotes(
        ListNotesRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new ListNotesResponse { Success = false, ErrorMessage = "Invalid user ID format." };

        Guid? folderId = Guid.TryParse(request.FolderId, out var fid) ? fid : null;
        var take = request.Take > 0 ? request.Take : 50;

        var results = await _noteService.ListNotesAsync(
            new CallerContext(userId, ["user"], CallerType.User),
            folderId, request.Skip, take,
            context.CancellationToken);

        var response = new ListNotesResponse { Success = true };
        response.Notes.AddRange(results.Select(ToNoteMessage));
        return response;
    }

    /// <inheritdoc />
    public override async Task<NoteResponse> UpdateNote(
        UpdateNoteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NoteId, out var noteId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new NoteResponse { Success = false, ErrorMessage = "Invalid ID format." };

        var dto = new UpdateNoteDto
        {
            FolderId = Guid.TryParse(request.FolderId, out var fid) ? fid : null,
            Title = NullIfEmpty(request.Title),
            Content = NullIfEmpty(request.Content),
            Format = Enum.TryParse<NoteContentFormat>(request.Format, true, out var fmt)
                ? fmt : null,
            IsPinned = bool.TryParse(request.IsPinned, out var isPinned) ? isPinned : null,
            IsFavorite = bool.TryParse(request.IsFavorite, out var isFav) ? isFav : null,
            Tags = request.UpdateTags ? request.Tags.ToList() : null,
            Links = request.UpdateLinks
                ? request.Links.Select(ToLinkDto).ToList()
                : null,
            ExpectedVersion = request.ExpectedVersion > 0 ? request.ExpectedVersion : null
        };

        try
        {
            var result = await _noteService.UpdateNoteAsync(
                noteId, dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new NoteResponse { Success = true, Note = ToNoteMessage(result) };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new NoteResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<DeleteNoteResponse> DeleteNote(
        DeleteNoteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NoteId, out var noteId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new DeleteNoteResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            await _noteService.DeleteNoteAsync(
                noteId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new DeleteNoteResponse { Success = true };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new DeleteNoteResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListNotesResponse> SearchNotes(
        SearchNotesRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new ListNotesResponse { Success = false, ErrorMessage = "Invalid user ID format." };

        var take = request.Take > 0 ? request.Take : 50;

        var results = await _noteService.SearchNotesAsync(
            new CallerContext(userId, ["user"], CallerType.User),
            NullIfEmpty(request.Query), request.Skip, take,
            context.CancellationToken);

        var response = new ListNotesResponse { Success = true };
        response.Notes.AddRange(results.Select(ToNoteMessage));
        return response;
    }

    /// <inheritdoc />
    public override async Task<FolderResponse> CreateFolder(
        CreateFolderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new FolderResponse { Success = false, ErrorMessage = "Invalid user ID format." };

        var dto = new CreateNoteFolderDto
        {
            ParentId = Guid.TryParse(request.ParentId, out var pid) ? pid : null,
            Name = request.Name,
            Color = NullIfEmpty(request.Color)
        };

        try
        {
            var result = await _folderService.CreateFolderAsync(
                dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new FolderResponse { Success = true, Folder = ToFolderMessage(result) };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new FolderResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListFoldersResponse> ListFolders(
        ListFoldersRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new ListFoldersResponse { Success = false, ErrorMessage = "Invalid user ID format." };

        Guid? parentId = Guid.TryParse(request.ParentId, out var pid) ? pid : null;

        var results = await _folderService.ListFoldersAsync(
            new CallerContext(userId, ["user"], CallerType.User), parentId, context.CancellationToken);

        var response = new ListFoldersResponse { Success = true };
        response.Folders.AddRange(results.Select(ToFolderMessage));
        return response;
    }

    /// <inheritdoc />
    public override async Task<VersionHistoryResponse> GetVersionHistory(
        GetVersionHistoryRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NoteId, out var noteId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new VersionHistoryResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            var versions = await _noteService.GetVersionHistoryAsync(
                noteId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

            var response = new VersionHistoryResponse { Success = true };
            response.Versions.AddRange(versions.Select(ToVersionMessage));
            return response;
        }
        catch (Exception ex) when (ex is Core.Errors.ValidationException)
        {
            return new VersionHistoryResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<NoteResponse> RestoreVersion(
        RestoreVersionRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NoteId, out var noteId) ||
            !Guid.TryParse(request.VersionId, out var versionId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new NoteResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            var result = await _noteService.RestoreVersionAsync(
                noteId, versionId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);
            return new NoteResponse { Success = true, Note = ToNoteMessage(result) };
        }
        catch (Exception ex) when (ex is Core.Errors.ValidationException)
        {
            return new NoteResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static NoteMessage ToNoteMessage(NoteDto dto)
    {
        var msg = new NoteMessage
        {
            Id = dto.Id.ToString(),
            OwnerId = dto.OwnerId.ToString(),
            FolderId = dto.FolderId?.ToString() ?? "",
            Title = dto.Title,
            Content = dto.Content,
            Format = dto.Format.ToString(),
            IsPinned = dto.IsPinned,
            IsFavorite = dto.IsFavorite,
            Version = dto.Version,
            Etag = dto.ETag ?? "",
            ContentLength = dto.ContentLength,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O")
        };
        msg.Tags.AddRange(dto.Tags);
        msg.Links.AddRange(dto.Links.Select(l => new NoteLinkMessage
        {
            LinkType = l.LinkType.ToString(),
            TargetId = l.TargetId.ToString(),
            DisplayLabel = l.DisplayLabel ?? ""
        }));
        return msg;
    }

    private static FolderMessage ToFolderMessage(NoteFolderDto dto)
    {
        return new FolderMessage
        {
            Id = dto.Id.ToString(),
            OwnerId = dto.OwnerId.ToString(),
            ParentId = dto.ParentId?.ToString() ?? "",
            Name = dto.Name,
            Color = dto.Color ?? "",
            SortOrder = dto.SortOrder,
            NoteCount = dto.NoteCount,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O")
        };
    }

    private static NoteVersionMessage ToVersionMessage(NoteVersionDto dto)
    {
        return new NoteVersionMessage
        {
            Id = dto.Id.ToString(),
            NoteId = dto.NoteId.ToString(),
            VersionNumber = dto.VersionNumber,
            Title = dto.Title,
            Content = dto.Content,
            EditedByUserId = dto.EditedByUserId.ToString(),
            CreatedAt = dto.CreatedAt.ToString("O")
        };
    }

    private static NoteLinkDto ToLinkDto(NoteLinkMessage msg)
    {
        return new NoteLinkDto
        {
            LinkType = Enum.TryParse<NoteLinkType>(msg.LinkType, true, out var lt)
                ? lt : NoteLinkType.Note,
            TargetId = Guid.TryParse(msg.TargetId, out var tid) ? tid : Guid.Empty,
            DisplayLabel = NullIfEmpty(msg.DisplayLabel)
        };
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
