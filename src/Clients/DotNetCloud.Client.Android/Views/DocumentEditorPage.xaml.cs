using DotNetCloud.Client.Android.Platforms;
using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Maui.Handlers;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Full-screen Collabora Online editor. Loads the server-minted editor URL top-level in a WebView:
/// the URL authenticates itself with its short-lived, file-scoped WOPI token, so no app cookie or
/// second login is involved.
/// </summary>
public partial class DocumentEditorPage : ContentPage
{
    private readonly DocumentEditorViewModel _vm;

    /// <summary>
    /// True once the current load has been reported as failed. Chromium renders its own error
    /// document for an HTTP error and then reports <see cref="WebNavigationResult.Success"/>, which
    /// would otherwise clear the error straight away.
    /// </summary>
    private bool _loadFailed;

    /// <summary>Initializes a new <see cref="DocumentEditorPage"/>.</summary>
    public DocumentEditorPage(DocumentEditorViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentEditorViewModel.EditorUrl) &&
                !string.IsNullOrEmpty(_vm.EditorUrl))
            {
                // Install before the source is assigned: MAUI's own mapper runs at handler-connect
                // time (after HandlerChanged) and would otherwise replace this client.
                _loadFailed = false;
                AttachHttpErrorReporting();
                EditorWebView.Source = new UrlWebViewSource { Url = _vm.EditorUrl };
            }
        };
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        AttachHttpErrorReporting();
    }

    /// <summary>
    /// Replaces the platform WebView client with one that also reports main-frame HTTP errors, so a
    /// Collabora proxy failure (for example a 502 while coolwsd is down) reaches the retry overlay
    /// instead of leaving Chromium's raw error page on screen. Safe to call more than once.
    /// </summary>
    private void AttachHttpErrorReporting()
    {
        if (EditorWebView.Handler is not WebViewHandler handler ||
            handler.PlatformView is not global::Android.Webkit.WebView native ||
            native.WebViewClient is EditorWebViewClient)
        {
            return;
        }

        native.SetWebViewClient(new EditorWebViewClient(handler, statusCode =>
        {
            _loadFailed = true;
            _vm.NotifyEditorLoadFailed(statusCode);
        }));
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Leaving by any route (close button, system back, shell navigation) releases the WOPI
        // concurrent-session slot. Idempotent, so an explicit Close is not sent twice.
        _ = _vm.EndSessionAsync();
    }

    private void OnEditorNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Success)
        {
            // Success is also reported for Chromium's own error document — keep the error visible.
            if (!_loadFailed)
                _vm.NotifyEditorLoaded();
        }
        else if (e.Result is WebNavigationResult.Failure or WebNavigationResult.Timeout)
        {
            _loadFailed = true;
            _vm.NotifyEditorLoadFailed();
        }
    }
}
