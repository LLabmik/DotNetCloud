using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the Collabora document editor component.
/// Manages WOPI token generation, editor iframe URL, co-editing indicators, and the
/// fullscreen toggle for the editor container.
/// </summary>
public partial class DocumentEditor : ComponentBase, IAsyncDisposable
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

    /// <summary>Injected for building absolute API URLs.</summary>
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>Accessor for capturing auth cookie during initialization.</summary>
    [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    /// <summary>Injected for the fullscreen toggle (see <c>wwwroot/js/document-editor.js</c>).</summary>
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>Reference to the editor container element that goes fullscreen.</summary>
    private ElementReference _containerRef;

    /// <summary>Callback handle passed to the JS helper so it can report fullscreen changes.</summary>
    private DotNetObjectReference<DocumentEditor>? _dotNetRef;

    /// <summary>Whether the fullscreen interop helper has been registered with the browser.</summary>
    private bool _fullscreenRegistered;

    /// <summary>Captured auth cookie from the initial HTTP request.</summary>
    private string? _capturedCookie;

    /// <summary>The editor iframe URL (set after successful token generation).</summary>
    protected string? EditorUrl { get; set; }

    /// <summary>Whether the component is loading (generating token).</summary>
    protected bool IsLoading { get; set; }

    /// <summary>Error message if editor initialization failed.</summary>
    protected string? ErrorMessage { get; set; }

    /// <summary>List of other users currently co-editing this document.</summary>
    protected List<string> CoEditingUsers { get; set; } = [];

    /// <summary>Whether the editor container currently holds browser fullscreen.</summary>
    protected bool IsFullscreen { get; private set; }

    /// <summary>Icon for the fullscreen toggle — swaps between the enter and exit states.</summary>
    protected string FullscreenIcon => DocumentEditorFullscreen.GetIcon(IsFullscreen);

    /// <summary>Tooltip / aria-label for the fullscreen toggle.</summary>
    protected string FullscreenTooltip => DocumentEditorFullscreen.GetTooltip(IsFullscreen);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _capturedCookie = HttpContextAccessor.HttpContext?.Request.Headers.Cookie.ToString();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RegisterFullscreenAsync();
        }
    }

    /// <summary>
    /// Hands the editor container to the JS fullscreen helper. Best-effort: a missing script
    /// only disables the fullscreen toggle, it never breaks the editor.
    /// </summary>
    private async Task RegisterFullscreenAsync()
    {
        _dotNetRef ??= DotNetObjectReference.Create(this);

        try
        {
            await Js.InvokeVoidAsync($"{DocumentEditorFullscreen.JsObject}.register", _containerRef, _dotNetRef);
            _fullscreenRegistered = true;
        }
        catch (Exception ex) when (IsJsInteropFailure(ex))
        {
            _fullscreenRegistered = false;
        }
    }

    /// <summary>
    /// Toggles browser fullscreen for the editor container. The resulting state arrives
    /// asynchronously through <see cref="OnFullscreenChanged"/> from the browser's
    /// <c>fullscreenchange</c> event, which also covers Esc and browser-initiated exits.
    /// </summary>
    protected async Task ToggleFullscreenAsync()
    {
        try
        {
            await Js.InvokeVoidAsync($"{DocumentEditorFullscreen.JsObject}.toggle");
        }
        catch (Exception ex) when (IsJsInteropFailure(ex))
        {
            // Best-effort: leave the button state untouched when the browser refuses or the
            // helper is unavailable.
        }
    }

    /// <summary>
    /// Called from the browser when the fullscreen state of the editor container changes.
    /// </summary>
    /// <param name="isFullscreen">Whether the editor container now holds fullscreen.</param>
    [JSInvokable(DocumentEditorFullscreen.ChangedCallback)]
    public Task OnFullscreenChanged(bool isFullscreen)
    {
        if (IsFullscreen == isFullscreen)
        {
            return Task.CompletedTask;
        }

        IsFullscreen = isFullscreen;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Drops the editor out of fullscreen. Used when the editor is closing, so the browser
    /// never stays fullscreen over the page behind it.
    /// </summary>
    private async Task ExitFullscreenAsync()
    {
        if (!_fullscreenRegistered || !IsFullscreen)
        {
            return;
        }

        try
        {
            await Js.InvokeVoidAsync($"{DocumentEditorFullscreen.JsObject}.exit");
        }
        catch (Exception ex) when (IsJsInteropFailure(ex))
        {
            // Best-effort: the circuit may already be gone.
        }
    }

    private static bool IsJsInteropFailure(Exception ex)
        => ex is JSException or JSDisconnectedException or InvalidOperationException or ObjectDisposedException;

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
            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            if (!string.IsNullOrEmpty(_capturedCookie))
            {
                request.Headers.TryAddWithoutValidation("Cookie", _capturedCookie);
            }
            var response = await Http.SendAsync(request);

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
        await ExitFullscreenAsync();

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
    /// Releases the fullscreen helper registration and the JS callback handle.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_fullscreenRegistered)
        {
            _fullscreenRegistered = false;

            try
            {
                await Js.InvokeVoidAsync($"{DocumentEditorFullscreen.JsObject}.dispose");
            }
            catch (Exception ex) when (IsJsInteropFailure(ex))
            {
                // Best-effort: the circuit or the script may already be gone.
            }
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
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
        // Prefer an explicit absolute ApiBaseUrl; otherwise use NavigationManager.BaseUri.
        if (Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var absolute))
            return $"{absolute.Scheme}://{absolute.Authority}{relativePath}";

        // Fall back to the server's own base URI so relative API calls work
        // even when ApiBaseUrl is a Blazor route (e.g., "/apps/files").
        var baseUri = Navigation.BaseUri.TrimEnd('/');
        return $"{baseUri}{relativePath}";
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
