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

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Trigger initialization via the ViewModel
        if (BindingContext is ImageViewerViewModel vm)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await vm.InitializeCommand.ExecuteAsync(null);
            });
        }
    }
}
