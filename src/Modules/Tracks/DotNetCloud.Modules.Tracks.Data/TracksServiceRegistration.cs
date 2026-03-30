using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Services;
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
        // Core business services
        services.AddScoped<ActivityService>();
        services.AddScoped<TeamService>();
        services.AddScoped<BoardService>();
        services.AddScoped<SwimlaneService>();
        services.AddScoped<CardService>();
        services.AddScoped<LabelService>();
        services.AddScoped<CommentService>();
        services.AddScoped<ChecklistService>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<DependencyService>();
        services.AddScoped<SprintService>();
        services.AddScoped<TimeTrackingService>();

        // Phase 4.7 services
        services.AddScoped<BoardTemplateService>();
        services.AddScoped<CardTemplateService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<SprintReportService>();
        services.AddScoped<BulkOperationService>();
        services.AddScoped<PokerService>();
        services.AddHostedService<DueDateReminderService>();

        // Real-time & notification services (Phase 4.6)
        services.AddSingleton<ITracksRealtimeService, TracksRealtimeService>();
        services.AddSingleton<ITracksNotificationService, TracksNotificationService>();
        services.AddSingleton<ITracksSignalRService, NullTracksSignalRService>();

        // Cross-module cleanup services
        services.AddScoped<ICardAttachmentCleanupService, CardAttachmentCleanupService>();

        return services;
    }
}
