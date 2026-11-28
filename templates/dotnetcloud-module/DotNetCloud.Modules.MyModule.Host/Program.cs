using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.MyModule;
using DotNetCloud.Modules.MyModule.Host.Services;
using DotNetCloud.Modules.Sdk;
using DotNetCloud.Modules.Sdk.Api;
using DotNetCloud.Modules.Sdk.Testing;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Bind the endpoint the supervisor assigned: a Unix socket, a named pipe or loopback TCP.
// Without this the host is not addressable by the core at all.
ModuleGrpcEndpointBinding.ConfigureModuleGrpcEndpoint(builder);

// Data Protection keys are private to the module. Never point this at the core's key ring.
var moduleDataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_DATA_DIR");
var keyDirectory = !string.IsNullOrWhiteSpace(moduleDataDir)
    ? Path.Combine(moduleDataDir, "data-protection-keys")
    : Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
Directory.CreateDirectory(keyDirectory);
builder.Services.AddDataProtection()
    .SetApplicationName("org.example.mymodule")
    .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));

// A sandboxed module receives only non-secret topology in module.json (engine, endpoint, quotas).
// Secrets never travel this way; anything secret is a settings value the operator sets.
var moduleConfigDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_CONFIG_DIR");
if (!string.IsNullOrEmpty(moduleConfigDir))
{
    var moduleJsonPath = Path.Combine(moduleConfigDir, "module.json");
    if (File.Exists(moduleJsonPath))
    {
        builder.Configuration.AddJsonFile(moduleJsonPath, optional: false, reloadOnChange: false);
    }
}

builder.Services.AddSingleton<MyModule>();

// One core client, one per-launch credential. When the host was not launched by the supervisor
// (a plain `dotnet run` while developing), the in-memory harness keeps it runnable without a core.
ModuleEnvironment.TryFromEnvironment(out var moduleEnvironment);
if (moduleEnvironment is not null)
{
    builder.Services.AddSingleton(moduleEnvironment);
    builder.Services.AddSingleton<IDncModuleApi>(_ => new DncModuleApiClient(moduleEnvironment));
}
else
{
    builder.Services.AddSingleton<IDncModuleApi, InMemoryDncModuleApi>();
}

builder.Services.AddGrpc(options =>
    options.Interceptors.Add<ModulePeerAuthenticationInterceptor>());
builder.Services.AddHealthChecks().AddCheck<MyModuleHealthCheck>("mymodule");

var app = builder.Build();

app.MapGrpcService<MyModuleLifecycleService>();
app.MapHealthChecks("/health");

// Liveness/version probe for operators. The module's own UI, when it has one, belongs at "/".
app.MapGet("/api/info", () => Results.Ok(new
{
    module = MyModuleManifest.ModuleId,
    version = MyModuleManifest.ModuleVersion,
    status = "running",
}));

await app.RunAsync();
