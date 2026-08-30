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
        if (_vm.Models.Count == 0)
            await _vm.LoadAsync();
    }

    /// <inheritdoc />
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        // Android soft-keyboard workaround (WindowSoftInputMode=AdjustResize): when the
        // keyboard shows, the window resizes and MAUI/Shell can drop the top chrome
        // (navbar + in-page header) during the re-measure, leaving the message list
        // rendered up under the status bar. Once the resize settles, force a layout
        // pass so the header and scroll area restore their correct positions.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120), () =>
        {
            (Content as IView)?.InvalidateMeasure();
            if (Shell.Current is IView shellView)
                shellView.InvalidateMeasure();
        });
    }

    /// <summary>Scrolls the message list to the bottom (new message or stream chunk).</summary>
    private void OnScrollRequested()
    {
        if (_vm.ActiveMessages.Count == 0)
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Let the layout settle before scrolling.
            await Task.Delay(30);
            if (MessagesList is not null)
                MessagesList.ScrollTo(_vm.ActiveMessages.Count - 1, position: ScrollToPosition.End, animate: false);
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
