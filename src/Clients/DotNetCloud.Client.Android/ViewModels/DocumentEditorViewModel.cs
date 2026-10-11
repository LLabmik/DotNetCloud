using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Files;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// ViewModel for the embedded Collabora Online editor. Mints a WOPI token for the file, exposes the
/// self-authenticating editor URL for the WebView to load, and ends the editing session when the page
/// is closed. Nothing is mirrored to the device — Collabora saves straight back to the server.
/// </summary>
public sealed partial class DocumentEditorViewModel : ObservableObject, IQueryAttributable
{
    private readonly IWopiRestClient _wopiApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<DocumentEditorViewModel> _logger;

    private Guid _nodeId;
    private bool _sessionEnded;

    /// <summary>Initializes a new <see cref="DocumentEditorViewModel"/>.</summary>
    public DocumentEditorViewModel(
        IWopiRestClient wopiApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<DocumentEditorViewModel> logger)
    {
        _wopiApi = wopiApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    /// <summary>File name shown in the page header.</summary>
    [ObservableProperty]
    private string _fileName = "Document";

    /// <summary>The minted editor URL. The page assigns it to the WebView source when it changes.</summary>
    [ObservableProperty]
    private string? _editorUrl;

    /// <summary>
    /// True while the token is being minted or the WebView is still loading the editor — drives the
    /// "Opening…" overlay so the user never sees a blank WebView.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowError))]
    private bool _isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowError))]
    private string? _errorMessage;

    /// <summary>True when the editor failed to open and the error overlay with Retry should show.</summary>
    public bool ShowError => !IsLoading && !string.IsNullOrEmpty(ErrorMessage);

    /// <inheritdoc />
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Query values arrive as strings: Shell's query system uses Convert.ChangeType, which cannot
        // handle Guid directly.
        if (query.TryGetValue("NodeId", out var nodeIdObj) && nodeIdObj is string nodeIdText &&
            Guid.TryParse(nodeIdText, out var nodeId))
        {
            _nodeId = nodeId;
        }

        if (query.TryGetValue("Name", out var nameObj) && nameObj is string name &&
            !string.IsNullOrWhiteSpace(name))
        {
            FileName = name;
        }

        MainThread.BeginInvokeOnMainThread(async () => await InitializeAsync(CancellationToken.None));
    }

    /// <summary>
    /// Mints a WOPI token and publishes the editor URL. Also the Retry action on the error overlay.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync(CancellationToken ct)
    {
        if (_nodeId == Guid.Empty)
        {
            IsLoading = false;
            ErrorMessage = "This document could not be identified.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        EditorUrl = null;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var session = await _wopiApi.OpenEditorAsync(serverUrl, token, _nodeId, ct);

            _sessionEnded = false;
            EditorUrl = session.EditorUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open the Collabora editor for node {NodeId}.", _nodeId);
            IsLoading = false;
            ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Called by the page once the editor document has rendered, clearing the overlay.</summary>
    public void NotifyEditorLoaded()
    {
        ErrorMessage = null;
        IsLoading = false;
    }

    /// <summary>Called by the page when the WebView failed to load the editor URL.</summary>
    /// <param name="httpStatusCode">
    /// HTTP status reported for the main document, or <c>0</c> for a transport-level failure.
    /// </param>
    public void NotifyEditorLoadFailed(int httpStatusCode = 0)
    {
        _logger.LogInformation(
            "Collabora editor load failed for node {NodeId} (HTTP status {StatusCode}).",
            _nodeId, httpStatusCode);

        IsLoading = false;
        ErrorMessage = httpStatusCode switch
        {
            0 => "The document editor could not be loaded. Check your connection and try again.",
            >= 500 => $"The document editor is unavailable right now (HTTP {httpStatusCode}). " +
                      "Collabora may be down — try again in a moment.",
            401 or 403 => "You no longer have access to this document.",
            404 => "The document could not be found on the server.",
            _ => $"The document editor could not be loaded (HTTP {httpStatusCode})."
        };
    }

    /// <summary>Closes the editor: ends the WOPI session (best effort), then pops the page.</summary>
    [RelayCommand]
    private async Task CloseAsync()
    {
        await EndSessionAsync();
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>Opens the editor URL in the system browser — the fallback when the WebView cannot.</summary>
    [RelayCommand]
    private async Task OpenInBrowserAsync()
    {
        if (string.IsNullOrEmpty(EditorUrl))
            return;

        try
        {
            await Launcher.Default.OpenAsync(EditorUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open the editor URL in the system browser.");
        }
    }

    /// <summary>
    /// Ends the WOPI session so the server frees this user's concurrent-session slot for the file.
    /// Idempotent and best effort: it never throws and never blocks leaving the editor.
    /// </summary>
    public async Task EndSessionAsync()
    {
        if (_sessionEnded || _nodeId == Guid.Empty)
            return;

        _sessionEnded = true;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            await _wopiApi.CloseEditorAsync(serverUrl, token, _nodeId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to end the WOPI session for node {NodeId}.", _nodeId);
        }
    }

    private async Task<(string ServerUrl, string Token)> GetCredentialsAsync(CancellationToken ct)
    {
        var connection = _serverStore.GetActive()
            ?? throw new InvalidOperationException("No active server connection.");
        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct)
            ?? throw new InvalidOperationException("No access token available.");
        return (connection.ServerBaseUrl, token);
    }
}
