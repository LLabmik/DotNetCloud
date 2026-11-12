using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Tracks.Services;
using DotNetCloud.Modules.Tracks.Web;
using DotNetCloud.Modules.Tracks.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Every request is relative: the core proxies /apps/dotnetcloud.tracks/ and strips the prefix, so the
// module host serves both the document and its API under the frame's own base address. The frame
// ticket is attached by the SDK bridge's fetch patch, not by a handler here.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});

builder.Services.AddScoped<TracksApi>();
builder.Services.AddSingleton<ModuleBridge>();

// The module's REST API, as the UI's API client.
builder.Services.AddScoped<ITracksApiClient, TracksHttpApiClient>();

// Capabilities the UI used to obtain from the core's DI container. In a sandboxed frame they come
// from the module host, which holds the grants: the browser never talks to the core directly.
builder.Services.AddScoped<IUserDirectory, ModuleUserDirectory>();
builder.Services.AddScoped<IOrganizationDirectory, ModuleOrganizationDirectory>();
builder.Services.AddScoped<ICommandPaletteService, BrowserCommandPalette>();
builder.Services.AddScoped<ICsvImportUiService, ModuleCsvImport>();
builder.Services.AddScoped<IOnboardingStateService, BrowserOnboardingState>();

// There is no live push channel into the frame yet (the core's SignalR hubs are not reachable from an
// opaque origin), so the UI runs on the no-op feeds: components subscribe to the same events and
// simply never receive them, which is exactly how the server runs when the hub is unavailable.
builder.Services.AddSingleton<ITracksSignalRService, NullTracksSignalRService>();
builder.Services.AddSingleton<IChatActivitySignalRService, NullChatActivitySignalRService>();

// The frame has no cookie of its own; the acting user is read from the module host once per page.
builder.Services.AddScoped<TracksAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TracksAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// Shared UI services the ported components resolve: the same implementations the in-core UI used.
builder.Services.AddScoped<DotNetCloud.UI.Shared.Services.BrowserTimeProvider>();
builder.Services.AddSingleton<DotNetCloud.UI.Shared.Services.IMarkdownRenderer, DotNetCloud.UI.Shared.Services.MarkdownRenderer>();

// localStorage throws in an opaque-origin frame; this keeps preferences session-only instead.
builder.Services.AddScoped<BrowserPreferenceStore>();

await builder.Build().RunAsync();
