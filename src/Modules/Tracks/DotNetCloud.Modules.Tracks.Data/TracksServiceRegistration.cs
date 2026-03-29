using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Tracks.Data;

/// <summary>
/// Registers Tracks module services for dependency injection.
/// </summary>
public static class TracksServiceRegistration
{
    /// <summary>
    /// Adds Tracks module services to the DI container.
    /// </summary>
    public static IServiceCollection AddTracksServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ActivityService>();
        services.AddScoped<BoardService>();
        services.AddScoped<ListService>();
        services.AddScoped<CardService>();
        services.AddScoped<LabelService>();
        services.AddScoped<CommentService>();
        services.AddScoped<ChecklistService>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<DependencyService>();
        services.AddScoped<SprintService>();
        services.AddScoped<TimeTrackingService>();

        return services;
    }
}
