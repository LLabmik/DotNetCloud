using System.Net.Http.Json;
using System.Text.Json;
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

    /// <summary>Base URL for the Files API (e.g., "https://cloud.example.com").</summary>
    [Parameter] public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Callback when the editor should be closed.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Callback to trigger file download instead of inline editing.</summary>
    [Parameter] public EventCallback OnDownload { get; set; }

    /// <summary>Injected HttpClient for calling the WOPI token endpoint.</summary>
    [Inject] private HttpClient Http { get; set; } = default!;

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
    /// Generates a WOPI access token by calling POST /api/v1/wopi/token/{fileId}
    /// and sets <see cref="EditorUrl"/> from the response.
    /// </summary>
    private async Task LoadEditorAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        StateHasChanged();

        try
        {
            var tokenEndpoint = BuildApiEndpoint($"/api/v1/wopi/token/{FileId}");
            var response = await Http.PostAsync(tokenEndpoint, content: null);

            if (!response.IsSuccessStatusCode)
            {
                var apiErrorMessage = await TryReadApiErrorMessageAsync(response);
                ErrorMessage = string.IsNullOrWhiteSpace(apiErrorMessage)
                    ? $"Could not open the document editor (HTTP {(int)response.StatusCode})."
                    : apiErrorMessage;
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<WopiTokenEnvelope>();
            EditorUrl = result?.Data?.EditorUrl;

            if (string.IsNullOrEmpty(EditorUrl))
            {
                ErrorMessage = "The document editor is not available for this file format.";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
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
    /// Triggers file download instead of inline editing and signals the editor to close.
    /// </summary>
    protected async Task DownloadInstead()
    {
        if (OnDownload.HasDelegate)
        {
            await OnDownload.InvokeAsync();
        }
    }

    /// <summary>
    /// Notifies the server that the editing session has ended, freeing a concurrent session slot.
    /// </summary>
    protected async Task CloseEditorAsync()
    {
        if (!string.IsNullOrEmpty(ApiBaseUrl) && FileId != Guid.Empty)
        {
            try
            {
                await Http.DeleteAsync(BuildApiEndpoint($"/api/v1/wopi/token/{FileId}"));
            }
            catch (HttpRequestException)
            {
                // Best-effort: session will expire naturally via the server-side timeout
            }
        }

        if (OnClose.HasDelegate)
        {
            await OnClose.InvokeAsync();
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

    /// <summary>
    /// Response envelope for the WOPI token endpoint.
    /// </summary>
    private sealed class WopiTokenEnvelope
    {
        public WopiTokenData? Data { get; set; }
    }

    /// <summary>
    /// Token data returned by POST /api/v1/wopi/token/{fileId}.
    /// </summary>
    private sealed class WopiTokenData
    {
        public string? EditorUrl { get; set; }
        public string? AccessToken { get; set; }
        public long AccessTokenTtl { get; set; }
        public string? WopiSrc { get; set; }
    }

    private string BuildApiEndpoint(string relativePath)
    {
        // ApiBaseUrl may arrive as an app route (e.g., /apps/files). When that happens,
        // force API calls to the host root to avoid false 404s.
        if (Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var absolute))
            return $"{absolute.Scheme}://{absolute.Authority}{relativePath}";

        return relativePath;
    }

    private static async Task<string?> TryReadApiErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var payload = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(payload))
                return null;

            var envelope = JsonSerializer.Deserialize<ApiErrorEnvelope>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return envelope?.Error?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class ApiErrorEnvelope
    {
        public ApiErrorDetails? Error { get; set; }
    }

    private sealed class ApiErrorDetails
    {
        public string? Message { get; set; }
    }
}
