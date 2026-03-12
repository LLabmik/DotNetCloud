using Android.App;
using Android.Runtime;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android application entry point. Creates the MAUI app via <see cref="MauiProgram.CreateMauiApp"/>.
/// </summary>
[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
