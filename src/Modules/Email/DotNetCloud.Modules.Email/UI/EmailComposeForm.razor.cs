using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Email;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Email.UI;

/// <summary>Code-behind for the <see cref="EmailComposeForm"/> component.</summary>
public partial class EmailComposeForm : ComponentBase
{
    [Parameter, EditorRequired] public required EmailSendRequest Compose { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyList<EmailAccount> Accounts { get; set; }
    [Parameter] public Func<EmailSendRequest, Task>? OnSend { get; set; }
    [Parameter] public Action? OnCancel { get; set; }
    [Parameter] public bool IsReply { get; set; }
    [Parameter] public bool IsSending { get; set; }

    private Guid? _selectedFromAccountId;
    private string _toInput = string.Empty;
    private string _ccInput = string.Empty;
    private string _bccInput = string.Empty;
    private string _subject = string.Empty;
    private string _bodyText = string.Empty;
    private bool _showCc;
    private bool _showBcc;

    // Attachment state
    private readonly List<ComposeAttachment> _attachments = [];
    private string? _uploadError;
    private bool _isUploading;
    private bool _showFilePicker;

    // Autocomplete state
    private string _activeAutocomplete = string.Empty;
    private List<ContactSearchResult> _toSuggestions = [];
    private List<ContactSearchResult> _ccSuggestions = [];
    private List<ContactSearchResult> _bccSuggestions = [];
    private int _toSelectedIndex;
    private int _ccSelectedIndex;
    private int _bccSelectedIndex;
    private CancellationTokenSource? _debounceCts;

    protected override void OnParametersSet()
    {
        if (Accounts.Count > 0 && _selectedFromAccountId is null)
            _selectedFromAccountId = Accounts[0].Id;

        if (Compose.To.Count > 0)
        {
            var first = Compose.To[0];
            _toInput = !string.IsNullOrWhiteSpace(first.Name)
                ? $"{first.Name} <{first.Email}>"
                : first.Email;
        }

        if (Compose.Cc is { Count: > 0 })
            _ccInput = string.Join(", ", Compose.Cc.Select(a => a.Name is not null ? $"{a.Name} <{a.Email}>" : a.Email));

        if (Compose.Bcc is { Count: > 0 })
            _bccInput = string.Join(", ", Compose.Bcc.Select(a => a.Name is not null ? $"{a.Name} <{a.Email}>" : a.Email));

        if (!string.IsNullOrWhiteSpace(Compose.Subject) && string.IsNullOrWhiteSpace(_subject))
            _subject = Compose.Subject;
    }

    private void ToggleCcBcc()
    {
        if (!_showCc && !_showBcc)
        {
            _showCc = true;
        }
        else if (_showCc && !_showBcc)
        {
            _showBcc = true;
        }
        else
        {
            _showCc = false;
            _showBcc = false;
        }
    }

    // ── Autocomplete ──────────────────────────────────────────────

    private async Task OnToInputChanged(ChangeEventArgs e)
    {
        _toInput = e.Value?.ToString() ?? string.Empty;
        await TriggerDebouncedSearch("to", _toInput);
    }

    private async Task OnCcInputChanged(ChangeEventArgs e)
    {
        _ccInput = e.Value?.ToString() ?? string.Empty;
        await TriggerDebouncedSearch("cc", _ccInput);
    }

    private async Task OnBccInputChanged(ChangeEventArgs e)
    {
        _bccInput = e.Value?.ToString() ?? string.Empty;
        await TriggerDebouncedSearch("bcc", _bccInput);
    }

    private async Task TriggerDebouncedSearch(string field, string input)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        // Get the last token for multi-recipient fields
        var query = GetLastRecipientToken(input);
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            ClearSuggestions(field);
            return;
        }

        try
        {
            await Task.Delay(300, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
            return;

        var caller = _currentUserContext.GetCurrentCaller();
        if (caller is null)
            return;

        try
        {
            var results = await _contactDirectory.SearchContactsWithEmailsAsync(
                caller.UserId, query, maxResults: 10, token);

            if (!token.IsCancellationRequested)
            {
                // Filter out already-added addresses
                var existingAddresses = ParseRecipients(input)
                    .Select(r => r.Email.ToLowerInvariant())
                    .ToHashSet();

                var filtered = results
                    .Select(r => new ContactSearchResult
                    {
                        ContactId = r.ContactId,
                        DisplayName = r.DisplayName,
                        Emails = r.Emails
                            .Where(e => !existingAddresses.Contains(e.Address.ToLowerInvariant()))
                            .ToList()
                    })
                    .Where(r => r.Emails.Count > 0)
                    .ToList();

                SetSuggestions(field, filtered);
                SetSelectedIndex(field, 0);
            }
        }
        catch
        {
            // Degrade gracefully if contact directory is unavailable
            ClearSuggestions(field);
        }
    }

    private static string GetLastRecipientToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var parts = input.Split(',', StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[^1] : string.Empty;
    }

    private void OnSuggestionKeydown(KeyboardEventArgs e, string field)
    {
        var suggestions = GetSuggestionsList(field);
        var selectedIndex = GetSelectedIndex(field);

        switch (e.Key)
        {
            case "ArrowDown":
                SetSelectedIndex(field, Math.Min(selectedIndex + 1, suggestions.Count - 1));
                break;
            case "ArrowUp":
                SetSelectedIndex(field, Math.Max(selectedIndex - 1, 0));
                break;
            case "Enter":
                if (selectedIndex >= 0 && selectedIndex < suggestions.Count)
                {
                    SelectSuggestion(suggestions[selectedIndex], field);
                }
                break;
            case "Escape":
                ClearSuggestions(field);
                break;
        }
    }

    private void SelectSuggestion(ContactSearchResult result, string field)
    {
        var currentInput = GetInputValue(field);
        var prefix = GetInputPrefix(currentInput);
        var primaryEmail = result.Emails.FirstOrDefault().Address;

        var entry = string.IsNullOrWhiteSpace(result.DisplayName)
            ? primaryEmail
            : $"{result.DisplayName} <{primaryEmail}>";

        var newValue = string.IsNullOrWhiteSpace(prefix)
            ? entry
            : $"{prefix}{entry}";

        SetInputValue(field, newValue);
        ClearSuggestions(field);
        StateHasChanged();
    }

    private void OnAutocompleteBlur(FocusEventArgs e)
    {
        // Small delay to allow suggestion click to register first
        _ = Task.Run(async () =>
        {
            await Task.Delay(150);
            await InvokeAsync(() =>
            {
                _activeAutocomplete = string.Empty;
                ClearSuggestions("to");
                ClearSuggestions("cc");
                ClearSuggestions("bcc");
                StateHasChanged();
            });
        });
    }

    // ── Autocomplete helpers ─────────────────────────────────────

    private string GetInputValue(string field) => field switch
    {
        "to" => _toInput,
        "cc" => _ccInput,
        "bcc" => _bccInput,
        _ => string.Empty
    };

    private void SetInputValue(string field, string value)
    {
        switch (field)
        {
            case "to": _toInput = value; break;
            case "cc": _ccInput = value; break;
            case "bcc": _bccInput = value; break;
        }
    }

    private static string GetInputPrefix(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var lastComma = input.LastIndexOf(',');
        if (lastComma < 0)
            return string.Empty;

        return input[..(lastComma + 1)] + " ";
    }

    private List<ContactSearchResult> GetSuggestionsList(string field) => field switch
    {
        "to" => _toSuggestions,
        "cc" => _ccSuggestions,
        "bcc" => _bccSuggestions,
        _ => []
    };

    private void SetSuggestions(string field, List<ContactSearchResult> suggestions)
    {
        switch (field)
        {
            case "to": _toSuggestions = suggestions; break;
            case "cc": _ccSuggestions = suggestions; break;
            case "bcc": _bccSuggestions = suggestions; break;
        }
    }

    private void ClearSuggestions(string field)
    {
        switch (field)
        {
            case "to": _toSuggestions = []; _toSelectedIndex = 0; break;
            case "cc": _ccSuggestions = []; _ccSelectedIndex = 0; break;
            case "bcc": _bccSuggestions = []; _bccSelectedIndex = 0; break;
        }
    }

    private int GetSelectedIndex(string field) => field switch
    {
        "to" => _toSelectedIndex,
        "cc" => _ccSelectedIndex,
        "bcc" => _bccSelectedIndex,
        _ => 0
    };

    private void SetSelectedIndex(string field, int index)
    {
        switch (field)
        {
            case "to": _toSelectedIndex = index; break;
            case "cc": _ccSelectedIndex = index; break;
            case "bcc": _bccSelectedIndex = index; break;
        }
    }

    private async Task OnFilesSelected(InputFileChangeEventArgs e)
    {
        _uploadError = null;
        const long maxSize = 25 * 1024 * 1024; // 25 MB

        foreach (var file in e.GetMultipleFiles(maximumFileCount: 10))
        {
            // Client-side size check
            if (file.Size > maxSize)
            {
                _uploadError = $"File \"{file.Name}\" exceeds the 25 MB limit. Large files can be shared via the Files module.";
                continue;
            }

            try
            {
                _isUploading = true;
                StateHasChanged();

                await using var stream = file.OpenReadStream(maxAllowedSize: maxSize);
                var result = await _apiClient.UploadAttachmentAsync(stream, file.Name, file.ContentType);

                _attachments.Add(new ComposeAttachment
                {
                    StorageKey = result.StorageKey,
                    FileName = result.FileName,
                    ContentType = result.ContentType,
                    Size = result.Size
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload attachment {FileName}", file.Name);
                _uploadError = $"Failed to upload \"{file.Name}\": {ex.Message}";
            }
            finally
            {
                _isUploading = false;
                StateHasChanged();
            }
        }
    }

    private void RemoveAttachment(ComposeAttachment att)
    {
        _attachments.Remove(att);
        StateHasChanged();
    }

    private void BrowseFiles()
    {
        _showFilePicker = true;
        StateHasChanged();
    }

    private async Task OnFilePickerResult(List<ComposeAttachment> selectedFiles)
    {
        _showFilePicker = false;
        _attachments.AddRange(selectedFiles);
        StateHasChanged();
    }

    private void CancelFilePicker()
    {
        _showFilePicker = false;
        StateHasChanged();
    }

    private async Task HandleSend()
    {
        _logger.LogInformation("HandleSend called. _toInput='{To}', _subject='{Subj}', _bodyText.Length={Len}, OnSend is null={Null}",
            _toInput, _subject, _bodyText?.Length ?? 0, OnSend is null);

        var recipients = ParseRecipients(_toInput);
        var ccList = ParseRecipients(_ccInput);
        var bccList = ParseRecipients(_bccInput);

        _logger.LogInformation("HandleSend parsed {Recipients} recipients", recipients.Count);

        var finalRequest = new EmailSendRequest
        {
            To = recipients,
            Subject = _subject,
            BodyPlainText = _bodyText,
            BodyHtml = null,
            Cc = ccList.Count > 0 ? ccList : null,
            Bcc = bccList.Count > 0 ? bccList : null,
            Attachments = _attachments.Count > 0
                ? _attachments.Select(a => new EmailAttachmentRef
                {
                    StorageKey = a.StorageKey,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    Size = a.Size
                }).ToList()
                : null
        };

        if (OnSend is not null)
        {
            _logger.LogInformation("HandleSend invoking OnSend callback");
            await OnSend.Invoke(finalRequest);
            _logger.LogInformation("HandleSend OnSend callback completed");
        }
        else
        {
            _logger.LogWarning("HandleSend: OnSend is null, cannot send");
        }
    }

    private static List<EmailAddressDto> ParseRecipients(string input)
    {
        var result = new List<EmailAddressDto>();
        foreach (var part in input.Split(',', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var match = System.Text.RegularExpressions.Regex.Match(part, @"^(.*?)\s*<(.+?)>\s*$");
            if (match.Success)
                result.Add(new EmailAddressDto { Name = match.Groups[1].Value.Trim().Trim('"'), Email = match.Groups[2].Value.Trim() });
            else
                result.Add(new EmailAddressDto { Name = null, Email = part.Trim() });
        }
        return result;
    }

    private static string GetComposeFileTypeIcon(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return "attach_file";
        var type = contentType.ToLowerInvariant();
        if (type.StartsWith("image/")) return "image";
        if (type == "application/pdf") return "picture_as_pdf";
        if (type is "application/zip" or "application/x-rar-compressed" or "application/gzip" or "application/x-7z-compressed" or "application/x-tar") return "folder_zip";
        if (type.StartsWith("text/")) return "text_snippet";
        if (type.StartsWith("audio/")) return "music_note";
        if (type.StartsWith("video/")) return "movie";
        if (type.Contains("spreadsheet") || type.Contains("excel") || type.Contains("csv")) return "table_chart";
        if (type.Contains("presentation") || type.Contains("powerpoint") || type.Contains("slides")) return "slideshow";
        if (type.Contains("word") || type.Contains("document")) return "article";
        return "attach_file";
    }

    private static string FormatComposeFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
