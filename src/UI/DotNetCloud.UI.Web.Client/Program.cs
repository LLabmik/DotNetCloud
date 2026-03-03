using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DotNetCloud.UI.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<ToastService>();

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<DotNetCloudApiClient>();

await builder.Build().RunAsync();
