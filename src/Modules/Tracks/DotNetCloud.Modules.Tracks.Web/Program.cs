using DotNetCloud.Modules.Tracks.Web;
using DotNetCloud.Modules.Tracks.Web.Services;
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

await builder.Build().RunAsync();
