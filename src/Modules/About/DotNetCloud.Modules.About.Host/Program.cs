using DotNetCloud.Modules.About;
using DotNetCloud.Modules.About.Host.Services;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<AboutModule>();

        builder.Services.AddGrpc();
        builder.Services.AddHealthChecks().AddCheck<AboutHealthCheck>("about_module");

        var app = builder.Build();

        app.MapGrpcService<AboutLifecycleService>();
        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Ok(new { module = "dotnetcloud.about", version = "1.0.0", status = "running" }));

        await app.RunAsync();
    }
}
