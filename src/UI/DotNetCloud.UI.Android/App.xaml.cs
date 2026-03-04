namespace DotNetCloud.UI.Android;

/// <summary>
/// MAUI Application class for the Android client.
/// </summary>
public class App : Application
{
    /// <summary>
    /// Initializes the app and creates the main window.
    /// </summary>
    public App()
    {
        MainPage = new MainPage();
    }
}
