using DotNetCloud.Modules.Calendar.Data.Services;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Calendar.Data;

/// <summary>
/// Extension methods for registering calendar services in the DI container.
/// </summary>
public static class CalendarServiceRegistration
{
    /// <summary>
    /// Registers all calendar service implementations in the DI container.
    /// </summary>
    public static IServiceCollection AddCalendarServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddScoped<ICalendarShareService, CalendarShareService>();
        services.AddScoped<IICalendarService, ICalService>();

        return services;
    }
}
