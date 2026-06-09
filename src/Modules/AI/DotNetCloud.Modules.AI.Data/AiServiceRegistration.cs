using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.AI.Data.Services;
using DotNetCloud.Modules.AI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.AI.Data;

/// <summary>
/// Registers AI module services for dependency injection.
/// </summary>
public static class AiServiceRegistration
{
    /// <summary>
    /// Adds AI module services to the DI container.
    /// </summary>
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register settings provider (reads from DB with IConfiguration fallback)
        services.AddScoped<IAiSettingsProvider, AiSettingsProvider>();

        // Register the LLM HTTP client with base address from configuration.
        // The base address set here is the startup default; the OllamaClient
        // uses IAiSettingsProvider at request time for dynamic reconfiguration.
        services.AddHttpClient<IOllamaClient, OllamaClient>((sp, client) =>
        {
            var baseUrl = configuration.GetValue<string>("AI:Ollama:BaseUrl") ?? "http://localhost:11434/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromMinutes(5); // LLM responses can be slow
        });

        services.AddScoped<IAiChatService, AiChatService>();

        return services;
    }

    /// <summary>
    /// Adds only the AI services needed by Blazor UI components rendered in Core.Server.
    /// AI has no hosted services, so this delegates to <see cref="AddAiServices"/>.
    /// </summary>
    public static IServiceCollection AddAiUiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        DatabaseProvider provider,
        string connectionString)
    {
        // Register AiDbContext for Blazor Server interactive rendering
        services.AddDbContext<AiDbContext>(options =>
            ModuleDbContextConfiguration.Configure(options, provider, connectionString, "DotNetCloud.Modules.AI.Data.SqlServer"),
            ServiceLifetime.Transient);

        services.AddScoped<IAiSettingsProvider, AiSettingsProvider>();
        services.AddHttpClient<IOllamaClient, OllamaClient>((sp, client) =>
        {
            var baseUrl = configuration.GetValue<string>("AI:Ollama:BaseUrl") ?? "http://localhost:11434/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddScoped<IAiChatService, AiChatService>();

        return services;
    }
}
