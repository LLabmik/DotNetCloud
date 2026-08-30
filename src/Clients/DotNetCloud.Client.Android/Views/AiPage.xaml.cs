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
