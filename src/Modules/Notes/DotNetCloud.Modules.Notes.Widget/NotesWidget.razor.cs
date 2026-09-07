using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Notes.Widget;

/// <summary>
/// Home-page widget for the Notes module: the five most recently edited notes for the
/// signed-in user (including notes shared with them).
/// </summary>
public partial class NotesWidget : ComponentBase
{
    [Inject] private INoteService NoteService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private readonly List<NoteDto> _items = [];
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No notes yet.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var caller = await BuildCallerAsync();
            var recent = await NoteService.GetRecentNotesAsync(caller, 5);
            _items.AddRange(recent);
        }
        catch (Exception)
        {
            _error = "Unable to load note data.";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Builds a deep link that opens the Notes module at the given note.
    /// </summary>
    private static string HrefFor(NoteDto item)
        => $"/apps/notes?noteId={item.Id}";

    private async Task<CallerContext> BuildCallerAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = state.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(userId, roles, CallerType.User);
    }
}
