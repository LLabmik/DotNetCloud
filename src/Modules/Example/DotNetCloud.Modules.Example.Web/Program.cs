using DotNetCloud.Modules.Example.Web;
using DotNetCloud.Modules.Example.Web.Services;
using DotNetCloud.Modules.Sdk.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Every request is relative: the core proxies /apps/dotnetcloud.example/ and strips the prefix, so
// the module host serves both the document and its API under the frame's own base address. The frame
// ticket is attached by the SDK bridge's fetch patch, not by a handler here.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});

builder.Services.AddScoped<ModuleApiClient>();
builder.Services.AddScoped<ExampleApi>();

// Frame contract with the core shell: theme, title, toasts and consent prompts.
builder.Services.AddSingleton<ModuleFrameBridge>();

// The frame has no cookie of its own; the acting user is read from the module host once per page.
builder.Services.AddScoped<ModuleFrameAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ModuleFrameAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

await builder.Build().RunAsync();
