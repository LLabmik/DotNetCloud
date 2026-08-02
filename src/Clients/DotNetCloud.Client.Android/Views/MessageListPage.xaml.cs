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
    private bool _initialized;

    /// <summary>
    /// Whether the user is currently at/near the newest messages. Used to keep the list
    /// pinned to the bottom when real-time messages arrive without yanking users who
    /// scrolled up to read history. Initialized true so short lists (and the initial
    /// load, which scrolls to the bottom) behave correctly before the first scroll event.
    /// </summary>
    private bool _isNearBottom = true;

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
        vm.NewMessageAdded += OnNewMessageAdded;
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

            // Set the initial scroll mode based on whether we think the user is near the
            // bottom. Kept alive across appearances (subscription survives detail-page pushes).
            MessageList.ItemsUpdatingScrollMode = _isNearBottom
                ? ItemsUpdatingScrollMode.KeepLastItemInView
                : ItemsUpdatingScrollMode.KeepScrollOffset;

            // Only initialize on first appearance — subsequent appearances (e.g., returning
            // from ImageViewer or ChannelDetails) must preserve scroll position. Real-time
            // messages arrive via the SignalR handler without a full reload.
            if (!_initialized)
            {
                _initialized = true;
                await _vm.InitializeAsync(_channelId, _channelDisplayName);
            }
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
        // Keep event subscriptions alive — OnDisappearing fires even when pushing a detail
        // page (ImageViewer, ChannelDetails). Tearing down subscriptions would lose scroll
        // position on return. The MessageListPage stays alive in the Shell navigation stack.
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
        // Track whether the user is at/near the newest messages so real-time messages
        // auto-scroll only when the user is already at/near the bottom, not while reading
        // history. Use a small look-ahead (3 items) and treat short lists as near-bottom
        // to stay robust when RecyclerView reports a slightly stale LastVisibleItemIndex.
        var isNearBottom = _vm.Messages.Count == 0
            || (e.LastVisibleItemIndex >= 0 && e.LastVisibleItemIndex >= _vm.Messages.Count - 3);
        if (isNearBottom != _isNearBottom)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MessageListPage] nearBottom {_isNearBottom} -> {isNearBottom} (lastVisible={e.LastVisibleItemIndex}, count={_vm.Messages.Count}, offset={e.VerticalOffset:F0})");
        }
        _isNearBottom = isNearBottom;

        // When the user is near the newest messages, let MAUI's KeepLastItemInView scroll
        // as items are appended (no manual ScrollTo hacks — MAUI handles RecyclerView
        // layout timing internally). When the user scrolls up to read history, switch back
        // to KeepScrollOffset so incoming messages don't yank the scroll position.
        MessageList.ItemsUpdatingScrollMode = _isNearBottom
            ? ItemsUpdatingScrollMode.KeepLastItemInView
            : ItemsUpdatingScrollMode.KeepScrollOffset;

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
                _isNearBottom = true;
                MessageList.ScrollTo(_vm.Messages.Count - 1, position: ScrollToPosition.End, animate: false);
            }
        });
    }

    /// <summary>
    /// Diagnostic handler — logs when a new message was appended so we can verify
    /// whether KeepLastItemInView auto-scrolled correctly (no manual ScrollTo needed).
    /// </summary>
    private void OnNewMessageAdded(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[MessageListPage] NewMessageAdded (isNearBottom={_isNearBottom}, count={_vm.Messages.Count}, mode={MessageList.ItemsUpdatingScrollMode})");
    }

    private async void OnViewDetailsRequested(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync(
                $"ChannelDetails?channelId={_channelId}&channelName={Uri.EscapeDataString(_channelDisplayName)}",
                animate: true));
    }
}
