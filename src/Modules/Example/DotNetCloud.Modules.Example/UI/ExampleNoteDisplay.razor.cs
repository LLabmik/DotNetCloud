using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Example.UI;

/// <summary>Code-behind for the <see cref="ExampleNoteDisplay"/> component.</summary>
public partial class ExampleNoteDisplay : ComponentBase
{
    [Parameter, EditorRequired]
    public ExampleNotesPage.NoteViewModel Note { get; set; } = default!;

    [Parameter]
    public EventCallback<Guid> OnDelete { get; set; }

    private async Task HandleDelete()
    {
        await OnDelete.InvokeAsync(Note.Id);
    }
}
