using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.TryAddSingleton<IBackgroundServiceTracker, BackgroundServiceTracker>();
        services.AddSingleton<TracksInProcessSignalRService>();
        services.AddSingleton<ITracksSignalRService>(sp => sp.GetRequiredService<TracksInProcessSignalRService>());
        services.AddSingleton<ITracksRealtimeService, TracksRealtimeService>();

        // HTTP client factory for outbound webhook deliveries (IHttpClientFactory)
        services.AddHttpClient("Webhooks", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

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
        // The command palette moved into the framed WASM UI (Tracks.Web.Services.BrowserCommandPalette);
        // there is no core-side palette RPC, so the server implementation was dead code.
        services.AddScoped<AutomationRuleService>();
        services.AddScoped<GoalService>();
        services.AddScoped<IAutomationRuleExecutionService, AutomationRuleExecutionService>();
        services.AddScoped<SprintDiscussionService>();

        // Cross-module services
        services.AddScoped<ICardAttachmentCleanupService, AttachmentCleanupService>();

        // Background services
        services.AddHostedService<ProductCleanupBackgroundService>();
        services.AddHostedService<RecurringWorkItemBackgroundService>();
        services.AddHostedService<WebhookRetryBackgroundService>();

        return services;
    }
}
