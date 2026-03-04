using System.Globalization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using DotNetCloud.Core.Localization;
using DotNetCloud.UI.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<ToastService>();

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<DotNetCloudApiClient>();

// Add localization services for i18n support
builder.Services.AddLocalization();

var host = builder.Build();

// Read culture from browser local storage; fall back to default
var js = host.Services.GetRequiredService<IJSRuntime>();
var storedCulture = await js.InvokeAsync<string>("blazorCulture.get");
var culture = CultureInfo.GetCultureInfo(storedCulture ?? SupportedCultures.DefaultCulture);

if (storedCulture is null)
{
    await js.InvokeVoidAsync("blazorCulture.set", SupportedCultures.DefaultCulture);
}

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
