using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.SharedWithMe;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Server.SharedWithMe;

/// <summary>
/// Adapts the Notes module's in-process <see cref="INoteService"/> into an
/// <see cref="ISharedWithMeProvider"/> so Files can surface shared notes as virtual deep-link
/// entries under <c>_DotNetCloud/SharedWithMe/Notes</c>.
/// </summary>
/// <remarks>
/// Lives in Core.Server (aggregation is a core concern) where the Notes UI services are registered
/// in-process. A fresh DI scope is created per call so the scoped <see cref="INoteService"/> (and its
/// scoped Notes DbContext) never outlives the request.
/// </remarks>
public sealed class NotesSharedWithMeProvider : ISharedWithMeProvider
{
    /// <summary>Stable module id for Notes.</summary>
    public const string NotesModuleId = "notes";

    /// <summary>Upper bound for how many visible notes are scanned for shared ones in one pass.</summary>
    private const int MaxScanNotes = 1000;

    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotesSharedWithMeProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a DI scope per call.</param>
    public NotesSharedWithMeProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public string ModuleId => NotesModuleId;

    /// <inheritdoc />
    public string DisplayName => "Notes";

    /// <inheritdoc />
    public string IconName => "edit_note";

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default)
        => (await ListAsync(userId, cancellationToken)).Count;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedWithMeModuleItem>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var noteService = scope.ServiceProvider.GetRequiredService<INoteService>();
        var caller = new CallerContext(userId, ["user"], CallerType.User);

        var notes = await noteService.ListNotesAsync(caller, take: MaxScanNotes, cancellationToken: cancellationToken);
        return MapItems(userId, notes);
    }

    /// <summary>
    /// Maps the notes visible to <paramref name="userId"/> to shared-with-me items. Notes the user
    /// owns and soft-deleted notes are excluded (only actual shares are surfaced).
    /// </summary>
    /// <param name="userId">The recipient user id.</param>
    /// <param name="notes">Notes the user can see (owned or shared).</param>
    internal static IReadOnlyList<SharedWithMeModuleItem> MapItems(Guid userId, IEnumerable<NoteDto> notes)
        => notes
            .Where(note => !note.IsDeleted && note.OwnerId != userId)
            .Select(note => new SharedWithMeModuleItem(
                ModuleId: NotesModuleId,
                DisplayName: "Notes",
                EntityId: note.Id,
                EntityType: "Note",
                Title: note.Title,
                Subtitle: null,
                DeepLink: $"/apps/notes?noteId={note.Id}",
                IconName: "edit_note",
                UpdatedAt: note.UpdatedAt))
            .ToList();
}
