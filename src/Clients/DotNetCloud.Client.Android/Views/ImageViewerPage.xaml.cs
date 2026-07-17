using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Full-screen image viewer page with swipe navigation, EXIF metadata panel,
/// and pinch-to-zoom support.
/// </summary>
public partial class ImageViewerPage : ContentPage
{
    // ── Per-gesture tracking state (only one gesture at a time) ──────
    private double _pinchStartScale = 1;

    /// <summary>Initializes a new <see cref="ImageViewerPage"/>.</summary>
    public ImageViewerPage(ImageViewerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
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
