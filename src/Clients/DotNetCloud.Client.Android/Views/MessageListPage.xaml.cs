using DotNetCloud.Client.Android.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Message list screen — real-time chat for a single channel.</summary>
[QueryProperty(nameof(ChannelId), "channelId")]
[QueryProperty(nameof(ChannelDisplayName), "channelName")]
public partial class MessageListPage : ContentPage
{
    private readonly MessageListViewModel _vm;
    private Guid _channelId;
    private string _channelDisplayName = string.Empty;
    private bool _scrollSubscribed;

    /// <summary>Injected channel ID from Shell navigation query parameter.</summary>
    public string ChannelId
    {
        set => _channelId = Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    /// <summary>Injected channel display name from Shell navigation query parameter.</summary>
    public string ChannelDisplayName
    {
        set => _channelDisplayName = value;
    }

    /// <summary>Initializes a new <see cref="MessageListPage"/>.</summary>
    public MessageListPage(MessageListViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        vm.ViewDetailsRequested += OnViewDetailsRequested;
        vm.OlderMessagesLoaded += OnOlderMessagesLoaded;
        vm.ScrollToBottomRequested += OnScrollToBottomRequested;
        vm.ScrollToMessageRequested += OnScrollToMessageRequested;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            // Subscribe to CollectionView.Scrolled for infinite scroll detection
            if (!_scrollSubscribed)
            {
                MessageList.Scrolled += OnMessageListScrolled;
                _scrollSubscribed = true;
            }

            await _vm.InitializeAsync(_channelId, _channelDisplayName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MessageListPage] OnAppearing error: {ex}");
        }
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.ScrollToBottomRequested -= OnScrollToBottomRequested;
        _vm.ScrollToMessageRequested -= OnScrollToMessageRequested;
        _vm.OlderMessagesLoaded -= OnOlderMessagesLoaded;
        if (_scrollSubscribed)
        {
            MessageList.Scrolled -= OnMessageListScrolled;
            _scrollSubscribed = false;
        }
    }

    /// <summary>
    /// Scrolls the message list to the message matching <paramref name="messageId"/>,
    /// centered in the viewport with animation.
    /// </summary>
    private void OnScrollToMessageRequested(object? sender, Guid messageId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            for (var i = 0; i < _vm.Messages.Count; i++)
            {
                if (_vm.Messages[i].Id == messageId)
                {
                    MessageList.ScrollTo(i, position: ScrollToPosition.Center, animate: true);
                    break;
                }
            }
        });
    }

    /// <summary>
    /// Detects when the user scrolls near the top of the message list
    /// and triggers loading of older messages (infinite scroll).
    /// </summary>
    private void OnMessageListScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        // When the first visible item is within the first few items and more pages exist,
        // but only if the initial load has already completed (not currently loading).
        if (e.FirstVisibleItemIndex <= 2 && _vm.HasMoreMessages && !_vm.IsLoadingMore && !_vm.IsLoading)
        {
            _vm.LoadMoreMessagesCommand.Execute(null);
        }
    }

    /// <summary>
    /// After older messages are prepended, restores scroll position to keep the
    /// previously-visible messages in view by scrolling to the anchor message.
    /// </summary>
    private void OnOlderMessagesLoaded(object? sender, Guid anchorMessageId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Find the new index of the anchor message in the updated collection
            for (var i = 0; i < _vm.Messages.Count; i++)
            {
                if (_vm.Messages[i].Id == anchorMessageId)
                {
                    MessageList.ScrollTo(i, position: ScrollToPosition.Start, animate: false);
                    break;
                }
            }
        });
    }

    /// <summary>
    /// Scrolls the message list to the bottom-most (newest) message.
    /// Fired after the initial message load completes.
    /// </summary>
    private void OnScrollToBottomRequested(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_vm.Messages.Count > 0)
            {
                MessageList.ScrollTo(_vm.Messages.Count - 1, position: ScrollToPosition.End, animate: false);
            }
        });
    }

    private async void OnViewDetailsRequested(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync(
                $"ChannelDetails?channelId={_channelId}&channelName={Uri.EscapeDataString(_channelDisplayName)}",
                animate: true));
    }
}
