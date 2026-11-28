using DotNetCloud.Modules.Example.Web.Services;
using DotNetCloud.Modules.Sdk.Web;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Example.Web.UI;

/// <summary>
/// Code-behind for the notes panel: list, create and delete the acting user's notes.
/// </summary>
/// <remarks>
/// Note text is user data and every reference below renders it as text, never as markup. Failures are
/// reported inline (an opaque-origin frame cannot open a modal dialog).
/// </remarks>
public partial class NotesPanel : ComponentBase
{
    private readonly List<ExampleNoteDto> _notes = [];
    private readonly HashSet<Guid> _deleting = [];

    private bool _loading = true;
    private bool _formOpen;
    private bool _saving;
    private string _title = string.Empty;
    private string _content = string.Empty;
    private string? _status;
    private string? _formError;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _status = null;

        try
        {
            var notes = await Api.ListNotesAsync();
            _notes.Clear();
            _notes.AddRange(notes);
        }
        catch (ModuleApiException ex) when (ex.IsUnauthorized)
        {
            _notes.Clear();
            _status = "Sign in to DotNetCloud to use this module.";
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _status = "Could not load notes. Try again.";
        }
        finally
        {
            _loading = false;
        }
    }

    private void OpenForm()
    {
        _formOpen = true;
        _formError = null;
    }

    private void CloseForm()
    {
        _formOpen = false;
        _title = string.Empty;
        _content = string.Empty;
        _formError = null;
    }

    private async Task CreateAsync()
    {
        _formError = null;

        if (string.IsNullOrWhiteSpace(_title))
        {
            _formError = "A title is required.";
            return;
        }

        _saving = true;
        try
        {
            await Api.CreateNoteAsync(_title.Trim(), _content);
            CloseForm();
            await LoadAsync();
            await Bridge.NotifyAsync("Note created");
        }
        catch (ModuleApiException ex) when (ex.StatusCode == 400)
        {
            _formError = ex.Message;
        }
        catch (ModuleApiException ex) when (ex.IsUnauthorized)
        {
            _formError = "Your session expired. Reload the page and sign in again.";
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _formError = "The note could not be saved. Try again.";
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task DeleteAsync(Guid noteId)
    {
        if (!_deleting.Add(noteId))
        {
            return;
        }

        try
        {
            await Api.DeleteNoteAsync(noteId);
            await LoadAsync();
            await Bridge.NotifyAsync("Note deleted");
        }
        catch (ModuleApiException ex) when (ex.IsUnauthorized)
        {
            _status = "Sign in to DotNetCloud to use this module.";
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _status = "The note could not be deleted. Try again.";
        }
        finally
        {
            _deleting.Remove(noteId);
        }
    }

    private static string FormatDate(DateTime value)
    {
        if (value == default)
        {
            return string.Empty;
        }

        var local = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
            : value.ToLocalTime();

        return local.ToString("g", System.Globalization.CultureInfo.CurrentCulture);
    }
}
