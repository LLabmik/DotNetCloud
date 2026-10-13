using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Full-screen image viewer page with swipe navigation, EXIF metadata panel,
/// and pinch-to-zoom support.
/// </summary>
public partial class ImageViewerPage : ContentPage
{
    private readonly ImageViewerViewModel _viewModel;
    private int _startIndex = -1;
    private bool _isStartIndexApplied;

    // ── Per-gesture tracking state (only one gesture at a time) ──────
    private double _pinchStartScale = 1;

    /// <summary>Initializes a new <see cref="ImageViewerPage"/>.</summary>
    public ImageViewerPage(ImageViewerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.StartIndexReady += OnStartIndexReady;

        // Fallback: if the index is known before the carousel has a platform view, apply it
        // as soon as the carousel is laid out.
        CarouselView.SizeChanged += OnCarouselSizeChanged;
    }

    /// <summary>Remembers which image the viewer should open on.</summary>
    private void OnStartIndexReady(object? sender, int startIndex)
    {
        _startIndex = startIndex;
        ApplyStartIndex();
    }

    private void OnCarouselSizeChanged(object? sender, EventArgs e) => ApplyStartIndex();

    /// <summary>
    /// Moves the carousel to the image the user tapped in the file browser.
    /// </summary>
    /// <remarks>
    /// Populating the item collection makes the Android carousel rebuild its adapter: it leaves
    /// CurrentItem on the first image, queues a scroll back to position 0 and then re-derives its
    /// position from that item during the next layout pass, which discards the requested start
    /// position. Steering the carousel explicitly once the items are loaded keeps the two in step,
    /// and the explicit scroll wins over the carousel's queued scroll to the first image.
    /// </remarks>
    private void ApplyStartIndex()
    {
        if (_isStartIndexApplied || _startIndex < 0 || CarouselView.Handler is null
            || _startIndex >= _viewModel.CarouselItems.Count)
        {
            return;
        }

        _isStartIndexApplied = true;

        CarouselView.CurrentItem = _viewModel.CarouselItems[_startIndex];
        CarouselView.ScrollTo(_startIndex, position: ScrollToPosition.Center, animate: false);
    }

    /// <summary>
    /// Handles pinch-to-zoom on the carousel image. Scales up to 5x around
    /// the focal point. Disables CarouselView swipe immediately on start so
    /// the two gesture systems don't compete for touch events.
    /// </summary>
    private void OnImagePinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (sender is not Image image)
            return;

        switch (e.Status)
        {
            case GestureStatus.Started:
                _pinchStartScale = image.Scale;
                CarouselView.IsSwipeEnabled = false;
                break;

            case GestureStatus.Running:
                var newScale = Math.Clamp(_pinchStartScale * e.Scale, 1.0, 5.0);
                image.Scale = newScale;

                // Zoom towards the pinch focal point
                var originX = (e.ScaleOrigin.X - 0.5) * 2;
                var originY = (e.ScaleOrigin.Y - 0.5) * 2;
                image.TranslationX = originX * image.Width * (newScale - 1) * 0.5;
                image.TranslationY = originY * image.Height * (newScale - 1) * 0.5;
                break;

            case GestureStatus.Completed:
                // Only reset to 1x if very close to baseline;
                // otherwise keep the zoom level
                if (image.Scale <= 1.05)
                {
                    image.Scale = 1.0;
                    image.TranslationX = 0;
                    image.TranslationY = 0;
                }

                CarouselView.IsSwipeEnabled = image.Scale <= 1.0;
                break;
        }
    }

    /// <summary>
    /// Double-tap toggles between 1x and 3x zoom, centered. If already
    /// zoomed, resets to 1x.
    /// </summary>
    private void OnImageDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Image image)
            return;

        if (image.Scale > 1.0)
        {
            // Reset zoom
            image.Scale = 1.0;
            image.TranslationX = 0;
            image.TranslationY = 0;
            CarouselView.IsSwipeEnabled = true;
        }
        else
        {
            // Zoom to 3x centered
            image.Scale = 3.0;
            image.TranslationX = 0;
            image.TranslationY = 0;
            CarouselView.IsSwipeEnabled = false;
        }
    }
}
