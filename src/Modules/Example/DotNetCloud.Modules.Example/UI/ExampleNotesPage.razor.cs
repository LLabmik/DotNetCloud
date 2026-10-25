using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Example.UI;

/// <summary>Code-behind for the <see cref="ExampleNotesPage"/> component.</summary>
public partial class ExampleNotesPage : ComponentBase
{
    private readonly List<NoteViewModel> _notes = [];
    private bool _showForm;

    private void ShowCreateForm() => _showForm = true;
    private void HideCreateForm() => _showForm = false;

    private void HandleNoteSaved(NoteViewModel note)
    {
        _notes.Insert(0, note);
        _showForm = false;
    }

    private void HandleNoteDeleted(Guid noteId)
    {
        _notes.RemoveAll(n => n.Id == noteId);
    }

    /// <summary>
    /// View model for displaying notes in the UI.
    /// </summary>
    public sealed record NoteViewModel
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }
}
