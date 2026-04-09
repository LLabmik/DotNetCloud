using DotNetCloud.Modules.AI.Data.Services;
using DotNetCloud.Modules.AI.Services;
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
}
