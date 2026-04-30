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
        // Real-time services (singletons — live for the lifetime of the process)
        services.AddSingleton<TracksInProcessSignalRService>();
        services.AddSingleton<ITracksSignalRService>(sp => sp.GetRequiredService<TracksInProcessSignalRService>());
        services.AddSingleton<ITracksRealtimeService, TracksRealtimeService>();

        // Data services (scoped — one per request, shares the scoped TracksDbContext)
        services.AddScoped<ProductService>();
        services.AddScoped<WorkItemService>();
        services.AddScoped<SprintService>();
        services.AddScoped<SprintPlanningService>();
        services.AddScoped<SwimlaneService>();
        services.AddScoped<SwimlaneTransitionService>();
        services.AddScoped<CommentService>();
        services.AddScoped<ChecklistService>();
        services.AddScoped<DependencyService>();
        services.AddScoped<TimeTrackingService>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<PokerService>();
        services.AddScoped<ReviewSessionService>();
        services.AddScoped<ActivityService>();
        services.AddScoped<ItemTemplateService>();
        services.AddScoped<ProductTemplateService>();
        services.AddScoped<CustomViewService>();
        services.AddScoped<CustomFieldService>();
        services.AddScoped<MilestoneService>();
        services.AddScoped<RecurringWorkItemService>();
        services.AddScoped<ShareLinkService>();
        services.AddScoped<GuestAccessService>();
        services.AddScoped<TemplateSeedService>();
        services.AddScoped<CsvImportService>();
        services.AddScoped<ICsvImportUiService, CsvImportUiService>();
        services.AddScoped<WebhookService>();
        services.AddScoped<WebhookDeliveryService>();
        services.AddScoped<IWebhookDispatchService, WebhookDispatchService>();
        services.AddScoped<ICommandPaletteService, CommandPaletteService>();
        services.AddScoped<AutomationRuleService>();
        services.AddScoped<GoalService>();
        services.AddScoped<IAutomationRuleExecutionService, AutomationRuleExecutionService>();

        // Cross-module services
        services.AddScoped<ICardAttachmentCleanupService, AttachmentCleanupService>();

        // Background services
        services.AddHostedService<ProductCleanupBackgroundService>();
        services.AddHostedService<RecurringWorkItemBackgroundService>();
        services.AddHostedService<WebhookRetryBackgroundService>();

        return services;
    }
}
