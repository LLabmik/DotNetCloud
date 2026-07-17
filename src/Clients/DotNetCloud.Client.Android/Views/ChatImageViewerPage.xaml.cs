namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Full-screen single-image viewer for chat image attachments.
/// Implements the official MAUI pinch-to-zoom and pan pattern from
/// Bugzilla57515 / PanGesturePlaygroundGallery samples.
/// </summary>
[QueryProperty(nameof(ImageUrl), "ImageUrl")]
[QueryProperty(nameof(FileName), "FileName")]
public partial class ChatImageViewerPage : ContentPage
{
    private string _imageUrl = string.Empty;

    // ── Gesture state (persists between gestures) ────────────────────
    private double _currentScale = 1;
    private double _xOffset, _yOffset;

    // ── Per-gesture tracking ─────────────────────────────────────────
    private double _startScale = 1;
    private double _panBaseX, _panBaseY;
    private bool _isPinching;

    /// <summary>Image URL to display (absolute).</summary>
    public string ImageUrl
    {
        get => _imageUrl;
        set
        {
            _imageUrl = value;
            LoadImage();
        }
    }

    /// <summary>Optional display name shown in the header.</summary>
    public string FileName
    {
        set => FileTitle.Text = value ?? string.Empty;
    }

    /// <summary>Initializes a new <see cref="ChatImageViewerPage"/>.</summary>
    public ChatImageViewerPage()
    {
        InitializeComponent();
    }

    private void LoadImage()
    {
        if (string.IsNullOrWhiteSpace(_imageUrl))
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                ChatImage.Source = ImageSource.FromUri(new Uri(_imageUrl));
                ChatImage.IsVisible = true;
                LoadingSpinner.IsRunning = false;
                LoadingSpinner.IsVisible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatImageViewerPage] Failed to load image: {ex}");
                LoadingSpinner.IsRunning = false;
                LoadingSpinner.IsVisible = false;
            }
        });
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>Single tap toggles the header and footer overlays together.</summary>
    private void OnImageTapped(object? sender, TappedEventArgs e)
    {
        var visible = !HeaderBar.IsVisible;
        HeaderBar.IsVisible = visible;
        FooterBar.IsVisible = visible;
    }

    // ── Double-tap: toggle 1x ↔ 3x centered ─────────────────────────

    private void OnImageDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_currentScale > 1.0)
        {
            _currentScale = 1.0;
            _xOffset = 0;
            _yOffset = 0;
        }
        else
        {
            _currentScale = 3.0;
            _xOffset = 0;
            _yOffset = 0;
        }
        ChatImage.Scale = _currentScale;
        ChatImage.TranslationX = _xOffset;
        ChatImage.TranslationY = _yOffset;
    }

    // ── Pinch-to-zoom (official MAUI pattern from Bugzilla57515) ─────

    private void OnImagePinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _startScale = _currentScale;
                // Per the official sample: set anchor to 0 so the scale
                // origin is the top-left corner, simplifying translation math.
                ChatImage.AnchorX = 0;
                ChatImage.AnchorY = 0;
                _isPinching = true;
                break;

            case GestureStatus.Running:
            {
                // MAUI's Android PinchGestureHandler computes:
                //   e.Scale = 1 + (androidScaleFactor - 1) * startingViewScale
                // The correct new scale is: startScale + (e.Scale - 1)
                // (NOT the sample's (e.Scale - 1) * startScale which over-applies)
                var newScale = Math.Clamp(_startScale + (e.Scale - 1), 1.0, 5.0);
                var scaleDelta = newScale - _startScale;

                // Official focal-point calculation (Bugzilla57515 / PanGesturePlaygroundGallery)
                // Adjusts ScaleOrigin by the current element position and starting scale
                // so the point under the user's fingers stays fixed during zoom.
                var renderedX = ChatImage.X + _xOffset;
                var deltaX = renderedX / Width;
                var deltaWidth = Width / (ChatImage.Width * _startScale);
                var originX = (e.ScaleOrigin.X - deltaX) * deltaWidth;

                var renderedY = ChatImage.Y + _yOffset;
                var deltaY = renderedY / Height;
                var deltaHeight = Height / (ChatImage.Height * _startScale);
                var originY = (e.ScaleOrigin.Y - deltaY) * deltaHeight;

                var targetX = _xOffset - (originX * ChatImage.Width) * scaleDelta;
                var targetY = _yOffset - (originY * ChatImage.Height) * scaleDelta;

                ChatImage.TranslationX = Math.Clamp(targetX, -ChatImage.Width * (newScale - 1), 0);
                ChatImage.TranslationY = Math.Clamp(targetY, -ChatImage.Height * (newScale - 1), 0);
                ChatImage.Scale = newScale;
                _currentScale = newScale;
                break;
            }

            case GestureStatus.Completed:
                _isPinching = false;
                // Persist translation offset for the next gesture
                _xOffset = ChatImage.TranslationX;
                _yOffset = ChatImage.TranslationY;
                if (_currentScale <= 1.05)
                {
                    _currentScale = 1.0;
                    _xOffset = 0;
                    _yOffset = 0;
                    ChatImage.Scale = 1.0;
                    ChatImage.TranslationX = 0;
                    ChatImage.TranslationY = 0;
                }
                break;
        }
    }

    // ── Download & Share ────────────────────────────────────────────

    /// <summary>Downloads the image to the device gallery.</summary>
    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_imageUrl))
                return;

            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(_imageUrl);

#if ANDROID
            // Save to the public Pictures directory so it's visible in the gallery
            var filename = $"DotNetCloud_{Guid.NewGuid():N}.jpg";
            var dir = global::Android.OS.Environment.GetExternalStoragePublicDirectory(
                global::Android.OS.Environment.DirectoryPictures)!.AbsolutePath;
            var path = System.IO.Path.Combine(dir, filename);
            await System.IO.File.WriteAllBytesAsync(path, data);

            // Notify the media scanner so the file shows up in the gallery immediately
            global::Android.Media.MediaScannerConnection.ScanFile(
                global::Android.App.Application.Context,
                new[] { path },
                new[] { "image/jpeg" },
                null);
#endif

            await DisplayAlertAsync("Downloaded", "Image saved to gallery.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatImageViewerPage] Download failed: {ex}");
            await DisplayAlertAsync("Error", "Failed to download image.", "OK");
        }
    }

    /// <summary>Shares the image file via the system share sheet.</summary>
    private async void OnShareClicked(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_imageUrl))
                return;

            // Download the image bytes first so we can share the actual file
            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(_imageUrl);

            var cacheDir = FileSystem.CacheDirectory;
            var tempFile = System.IO.Path.Combine(cacheDir, "share_image.jpg");
            await System.IO.File.WriteAllBytesAsync(tempFile, data);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = FileTitle.Text ?? "Image",
                File = new ShareFile(tempFile),
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatImageViewerPage] Share failed: {ex}");
        }
    }

    // ── Pan (official MAUI pattern from PanGesturePlaygroundGallery) ─

    private void OnImagePanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_isPinching)
            return;
        if (_currentScale <= 1.0)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panBaseX = _xOffset;
                _panBaseY = _yOffset;
                break;

            case GestureStatus.Running:
            {
                var maxX = Math.Max(0, (ChatImage.Width * _currentScale - ChatImage.Width) * 0.5);
                var maxY = Math.Max(0, (ChatImage.Height * _currentScale - ChatImage.Height) * 0.5);

                _xOffset = Math.Clamp(_panBaseX + e.TotalX, -maxX, maxX);
                _yOffset = Math.Clamp(_panBaseY + e.TotalY, -maxY, maxY);

                ChatImage.TranslationX = _xOffset;
                ChatImage.TranslationY = _yOffset;
                break;
            }

            case GestureStatus.Completed:
                // _xOffset/_yOffset already updated during Running
                break;
        }
    }
}
