using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the Collabora document editor component.
/// Manages WOPI token generation, editor iframe URL, and co-editing indicators.
/// </summary>
public partial class DocumentEditor : ComponentBase
{
    /// <summary>The file node ID to open in the editor.</summary>
    [Parameter] public Guid FileId { get; set; }

    /// <summary>The file name (for display).</summary>
    [Parameter] public string FileName { get; set; } = string.Empty;

    /// <summary>The current user ID (for token generation).</summary>
    [Parameter] public Guid UserId { get; set; }

    /// <summary>Base URL for the Files API (e.g., "https://cloud.example.com").</summary>
    [Parameter] public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Callback when the editor should be closed.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Callback to trigger file download instead of inline editing.</summary>
    [Parameter] public EventCallback OnDownload { get; set; }

    /// <summary>The editor iframe URL (set after successful token generation).</summary>
    protected string? EditorUrl { get; set; }

    /// <summary>Whether the component is loading (generating token).</summary>
    protected bool IsLoading { get; set; }

    /// <summary>Error message if editor initialization failed.</summary>
    protected string? ErrorMessage { get; set; }

    /// <summary>List of other users currently co-editing this document.</summary>
    protected List<string> CoEditingUsers { get; set; } = [];

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (FileId != Guid.Empty && string.IsNullOrEmpty(EditorUrl))
        {
            await LoadEditorAsync();
        }
    }

    /// <summary>
    /// Generates a WOPI access token and constructs the editor URL.
    /// </summary>
    private async Task LoadEditorAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        StateHasChanged();

        try
        {
            // In a full implementation, this would call the WOPI token endpoint via HttpClient:
            // POST /api/v1/wopi/token/{fileId}?userId={userId}
            // Response: { success: true, data: { accessToken, accessTokenTtl, wopiSrc, editorUrl } }
            //
            // For now, construct the URL pattern that will be used:
            var tokenEndpoint = $"{ApiBaseUrl.TrimEnd('/')}/api/v1/wopi/token/{FileId}?userId={UserId}";

            // Placeholder: in a real Blazor app this would use an injected HttpClient
            // var response = await Http.PostAsync(tokenEndpoint, null);
            // var result = await response.Content.ReadFromJsonAsync<WopiTokenResponse>();
            // EditorUrl = result.Data.EditorUrl;

            // Signal that the component is ready but needs real HTTP integration
            EditorUrl = null;
            ErrorMessage = null;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to open document editor: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Retries loading the editor after an error.
    /// </summary>
    protected async Task RetryAsync()
    {
        EditorUrl = null;
        await LoadEditorAsync();
    }

    /// <summary>
    /// Triggers file download instead of inline editing.
    /// </summary>
    protected async Task DownloadInstead()
    {
        if (OnDownload.HasDelegate)
        {
            await OnDownload.InvokeAsync();
        }
    }

    /// <summary>
    /// Checks whether a file extension is supported for online editing.
    /// </summary>
    public static bool IsSupportedForEditing(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        var ext = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();

        return ext is
            // Writer
            "doc" or "docx" or "odt" or "rtf" or "txt" or
            // Calc
            "xls" or "xlsx" or "ods" or "csv" or
            // Impress
            "ppt" or "pptx" or "odp";
    }
}
