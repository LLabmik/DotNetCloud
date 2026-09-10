namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Reports genuine user interaction to the presence service so the signed-in user's presence
/// dot stays green while they actively use the app. Only REAL interaction should call
/// <see cref="NotifyInteraction"/> — never transport keepalives. When the user stops
/// interacting (app backgrounded or left idle on a page), no further reports are sent and the
/// server flips them to Away/yellow after the idle threshold.
/// </summary>
public interface IActivityReporter
{
    /// <summary>
    /// Notifies the reporter that the user interacted (tap/key/scroll/nav/send). Implementations
    /// throttle before reporting to the server (~1 report / 20–30 s).
    /// </summary>
    void NotifyInteraction();
}
