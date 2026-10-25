using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Example.UI;

/// <summary>Code-behind for the <see cref="ExampleNoteForm"/> component.</summary>
public partial class ExampleNoteForm : ComponentBase
{
    [Parameter]
    public EventCallback<ExampleNotesPage.NoteViewModel> OnSave { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private string _title = string.Empty;
    private string _content = string.Empty;
    private string? _error;
    private bool _saving;

    private async Task HandleSave()
    {
        _error = null;

        if (string.IsNullOrWhiteSpace(_title))
        {
            _error = "Title is required.";
            return;
        }

        _saving = true;

        try
        {
            var note = new ExampleNotesPage.NoteViewModel
            {
                Id = Guid.NewGuid(),
                Title = _title.Trim(),
                Content = _content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await OnSave.InvokeAsync(note);

            _title = string.Empty;
            _content = string.Empty;
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task HandleCancel()
    {
        await OnCancel.InvokeAsync();
    }
}
