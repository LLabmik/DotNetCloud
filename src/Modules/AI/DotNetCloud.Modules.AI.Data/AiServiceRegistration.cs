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
        // Register the Ollama HTTP client with base address from configuration
        services.AddHttpClient<IOllamaClient, OllamaClient>((sp, client) =>
        {
            var baseUrl = configuration.GetValue<string>("AI:Ollama:BaseUrl") ?? "http://monolith.kimball.home:11434";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromMinutes(5); // LLM responses can be slow
        });

        services.AddScoped<IAiChatService, AiChatService>();

        return services;
    }
}
