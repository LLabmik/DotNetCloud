using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;
using System.Threading.Channels;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// An Android <see cref="ContentObserver"/> that watches <see cref="MediaStore"/>
/// for new photos and videos and signals a background scan via a <see cref="Channel{T}"/>.
/// Coalesces rapid-fire change events (burst photos) with a debounce window.
/// </summary>
internal sealed class MediaStoreContentObserver : ContentObserver
{
    private const int DebounceMs = 30_000;

    private readonly Channel<bool> _signalChannel;
    private CancellationTokenSource? _debounceCts;

    /// <summary>
    /// Initializes a new <see cref="MediaStoreContentObserver"/>.
    /// </summary>
    /// <param name="handler">Optional handler (pass null — we use our own onChange).</param>
    public MediaStoreContentObserver(Handler? handler = null)
        : base(handler)
    {
        _signalChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true
        });
    }

    /// <summary>
    /// A reader channel that receives a signal each time new media content is detected.
    /// The signal is debounced: rapid-fire changes within 30 seconds are coalesced into one.
    /// </summary>
    public ChannelReader<bool> Reader => _signalChannel.Reader;

    /// <inheritdoc />
    public override void OnChange(bool selfChange)
    {
        OnChange(selfChange, null);
    }

    /// <inheritdoc />
    public override void OnChange(bool selfChange, global::Android.Net.Uri? uri)
    {
        base.OnChange(selfChange, uri);

        // Cancel any pending debounce timer and start a new one.
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();

        var token = _debounceCts.Token;
        _ = DebounceAndSignalAsync(token);
    }

    /// <summary>
    /// Registers this observer on both image and video MediaStore content URIs.
    /// </summary>
    /// <param name="contentResolver">The application's <see cref="ContentResolver"/>.</param>
    public void Register(ContentResolver contentResolver)
    {
        var imagesUri = MediaStore.Images.Media.ExternalContentUri;
        if (imagesUri is not null)
            contentResolver.RegisterContentObserver(imagesUri, notifyForDescendants: true, this);

        var videosUri = MediaStore.Video.Media.ExternalContentUri;
        if (videosUri is not null)
            contentResolver.RegisterContentObserver(videosUri, notifyForDescendants: true, this);
    }

    /// <summary>
    /// Unregisters this observer from all MediaStore content URIs.
    /// </summary>
    /// <param name="contentResolver">The application's <see cref="ContentResolver"/>.</param>
    public void Unregister(ContentResolver contentResolver)
    {
        contentResolver.UnregisterContentObserver(this);
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }

    /// <summary>
    /// Waits for the debounce window, then writes a signal to the channel.
    /// If cancelled by a newer onChange call, the old signal is silently dropped.
    /// </summary>
    private async Task DebounceAndSignalAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(DebounceMs, ct).ConfigureAwait(false);
            // Channel is bounded(1) with DropOldest, so writing when full is safe.
            _signalChannel.Writer.TryWrite(true);
        }
        catch (System.OperationCanceledException)
        {
            // A newer onChange arrived — this debounce window was superseded.
        }
    }
}
