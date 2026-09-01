using DotNetCloud.Client.Android.Ai;
using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// AI Assistant page. Shows the conversation list and a streaming chat view
/// with lightweight Markdown rendering.
/// </summary>
public partial class AiPage : ContentPage
{
    private readonly AiViewModel _vm;

    /// <summary>Initializes a new <see cref="AiPage"/>.</summary>
    public AiPage(AiViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.ScrollRequested += OnScrollRequested;
        _vm.RenameRequested += OnRenameRequested;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Conversations.Count == 0)
            await _vm.LoadAsync();
    }

    /// <summary>
    /// Collapses the soft keyboard when Send is tapped. While the keyboard is open, the
    /// Shell/MAUI edge-to-edge layout renders the message list behind the status bar
    /// (WindowSoftInputMode=AdjustResize); dismissing the keyboard on send immediately
    /// restores the normal layout.
    /// </summary>
    private void OnSendClicked(object? sender, EventArgs e)
    {
        ComposerEditor?.Unfocus();

        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        var inputMethodManager = activity?.GetSystemService(global::Android.Content.Context.InputMethodService)
            as global::Android.Views.InputMethods.InputMethodManager;
        if (inputMethodManager is null)
            return;

        var token = (ComposerEditor?.Handler?.PlatformView as global::Android.Views.View)?.WindowToken
            ?? activity?.Window?.DecorView?.WindowToken;
        if (token is not null)
            inputMethodManager.HideSoftInputFromWindow(token, global::Android.Views.InputMethods.HideSoftInputFlags.None);
    }

    /// <summary>
    /// Scrolls the message list and the streaming output to the bottom so the latest
    /// generated text is always visible without manual scrolling.
    /// </summary>
    private void OnScrollRequested()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Let the layout settle before scrolling.
            await Task.Delay(30);
            if (_vm.ActiveMessages.Count > 0 && MessagesList is not null)
                MessagesList.ScrollTo(_vm.ActiveMessages.Count - 1, position: ScrollToPosition.End, animate: false);
            if (StreamScroll is not null)
                await StreamScroll.ScrollToAsync(0, double.MaxValue, false);
        });
    }

    /// <summary>Prompts for a new title and commits the rename.</summary>
    private async void OnRenameRequested(AiConversationDto conversation)
    {
        var title = await DisplayPromptAsync(
            "Rename conversation",
            "Enter a new title:",
            initialValue: conversation.Title,
            maxLength: 200);

        if (!string.IsNullOrWhiteSpace(title))
            await _vm.CommitRenameAsync(title);
    }
}
