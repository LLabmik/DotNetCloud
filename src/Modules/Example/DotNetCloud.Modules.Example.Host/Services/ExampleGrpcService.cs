using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Example.Host.Protos;
using DotNetCloud.Modules.Example.Data;
using DotNetCloud.Modules.Example.Models;
using DotNetCloud.Modules.Sdk.Api;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AuditAction = DotNetCloud.Core.Capabilities.AuditAction;

namespace DotNetCloud.Modules.Example.Host.Services;

/// <summary>
/// gRPC service implementation for the Example module.
/// Demonstrates how modules expose domain logic over gRPC for the core server to invoke.
/// </summary>
public sealed class ExampleGrpcService : ExampleService.ExampleServiceBase
{
    private readonly ExampleDbContext _db;
    private readonly IDncModuleApi _coreApi;
    private readonly ILogger<ExampleGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExampleGrpcService"/> class.
    /// </summary>
    public ExampleGrpcService(ExampleDbContext db, IDncModuleApi coreApi, ILogger<ExampleGrpcService> logger)
    {
        _db = db;
        _coreApi = coreApi;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<CreateNoteResponse> CreateNote(
        CreateNoteRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new CreateNoteResponse
            {
                Success = false,
                ErrorMessage = "Title is required."
            };
        }

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new CreateNoteResponse
            {
                Success = false,
                ErrorMessage = "Invalid user ID format."
            };
        }

        var note = new ExampleNote
        {
            Title = request.Title,
            Content = request.Content,
            CreatedByUserId = userId
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync(context.CancellationToken);

        // Attribution comes from the transport, never from the request: the core stamps the
        // authenticated module and the caller it verified. The payload's user id is domain data
        // (who owns the note), not an audit identity.
        await _coreApi.LogAuditAsync(
            AuditAction.Create,
            "ExampleNote",
            note.Id,
            "create-note",
            context.CancellationToken);

        _logger.LogInformation("Note {NoteId} created by user {UserId}", note.Id, userId);

        return new CreateNoteResponse
        {
            Success = true,
            Note = ToMessage(note)
        };
    }

    /// <inheritdoc />
    public override async Task<GetNoteResponse> GetNote(
        GetNoteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NoteId, out var noteId))
        {
            return new GetNoteResponse { Found = false };
        }

        var note = await _db.Notes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == noteId, context.CancellationToken);

        if (note is null)
        {
            return new GetNoteResponse { Found = false };
        }

        return new GetNoteResponse
        {
            Found = true,
            Note = ToMessage(note)
        };
    }

    /// <inheritdoc />
    public override async Task<ListNotesResponse> ListNotes(
        ListNotesRequest request, ServerCallContext context)
    {
        var response = new ListNotesResponse();

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return response;
        }

        var notes = await _db.Notes
            .AsNoTracking()
            .Where(n => n.CreatedByUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(context.CancellationToken);

        response.Notes.AddRange(notes.Select(ToMessage));
        return response;
    }

    /// <inheritdoc />
    public override async Task<DeleteNoteResponse> DeleteNote(
        DeleteNoteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NoteId, out var noteId))
        {
            return new DeleteNoteResponse
            {
                Success = false,
                ErrorMessage = "Invalid note ID format."
            };
        }

        var note = await _db.Notes.FindAsync([noteId], context.CancellationToken);

        if (note is null)
        {
            return new DeleteNoteResponse
            {
                Success = false,
                ErrorMessage = "Note not found."
            };
        }

        _db.Notes.Remove(note);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Note {NoteId} deleted", noteId);

        return new DeleteNoteResponse { Success = true };
    }

    private static NoteMessage ToMessage(ExampleNote note)
    {
        return new NoteMessage
        {
            Id = note.Id.ToString(),
            Title = note.Title,
            Content = note.Content,
            CreatedByUserId = note.CreatedByUserId.ToString(),
            CreatedAt = note.CreatedAt.ToString("O"),
            UpdatedAt = note.UpdatedAt?.ToString("O") ?? string.Empty
        };
    }
}
