using DotNetCloud.Client.Android.ViewModels;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Full-screen image viewer page with swipe navigation, EXIF metadata panel,
/// and pinch-to-zoom support.
/// </summary>
public partial class ImageViewerPage : ContentPage
{
    /// <summary>Initializes a new <see cref="ImageViewerPage"/>.</summary>
    public ImageViewerPage(ImageViewerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
