using Foundation;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Mac Catalyst application delegate. Creates the MAUI app via <see cref="MauiProgram.CreateMauiApp"/>.
/// </summary>
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
