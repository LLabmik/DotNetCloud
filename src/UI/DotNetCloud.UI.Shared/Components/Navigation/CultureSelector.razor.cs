using System.Globalization;
using DotNetCloud.Core.Localization;
using Microsoft.JSInterop;

namespace DotNetCloud.UI.Shared.Components.Navigation;

/// <summary>Code-behind for the <see cref="CultureSelector"/> component.</summary>
public partial class CultureSelector
{
    private readonly CultureInfo[] supportedCultures = SupportedCultures.GetCultureInfos();
    private CultureInfo? selectedCulture;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        selectedCulture = CultureInfo.CurrentCulture;
    }

    private async Task ApplySelectedCultureAsync()
    {
        if (CultureInfo.CurrentCulture == selectedCulture)
        {
            return;
        }

        // Persist in browser local storage (for WASM startup)
        await JS.InvokeVoidAsync("blazorCulture.set", selectedCulture!.Name);

        // Redirect through CultureController to set the server-side cookie
        var uri = new Uri(Navigation.Uri)
            .GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped);
        var cultureEscaped = Uri.EscapeDataString(selectedCulture.Name);
        var uriEscaped = Uri.EscapeDataString(uri);

        Navigation.NavigateTo(
            $"Culture/Set?culture={cultureEscaped}&redirectUri={uriEscaped}",
            forceLoad: true);
    }
}
