using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Import;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Data.Services;
using DotNetCloud.Modules.Notes.Services;
using DotNetCloud.UI.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Notes.Data;

/// <summary>
/// Registers Notes module services for dependency injection.
/// </summary>
public static class NotesServiceRegistration
{
    /// <summary>
    /// Adds Notes module services to the DI container.
    /// </summary>
    public static IServiceCollection AddNotesServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<INoteFolderService, NoteFolderService>();
        services.AddScoped<INoteShareService, NoteShareService>();
        services.AddScoped<IImportProvider, NotesImportProvider>();
        return services;
    }

    /// <summary>
    /// Adds only the Notes services needed by Blazor UI components rendered in Core.Server.
    /// Includes the NotesDbContext registration. Excludes background services.
    /// </summary>
    public static IServiceCollection AddNotesUiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        DatabaseProvider provider,
        string connectionString)
    {
        // Register NotesDbContext for Blazor Server interactive rendering
        services.AddDbContext<NotesDbContext>(options =>
            ModuleDbContextConfiguration.Configure(options, provider, connectionString, "DotNetCloud.Modules.Notes.Data.SqlServer"),
            ServiceLifetime.Transient);

        services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<INoteFolderService, NoteFolderService>();
        services.AddScoped<INoteShareService, NoteShareService>();
        services.AddScoped<IImportProvider, NotesImportProvider>();
        return services;
    }
}
