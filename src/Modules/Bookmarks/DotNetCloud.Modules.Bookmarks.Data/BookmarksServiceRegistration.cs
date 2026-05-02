using DotNetCloud.Modules.Bookmarks.Data.Services;
using DotNetCloud.Modules.Bookmarks.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Bookmarks.Data;

/// <summary>
/// DI registration for the Bookmarks module.
/// </summary>
public static class BookmarksServiceRegistration
{
    /// <summary>
    /// Registers all Bookmarks module services.
    /// </summary>
    public static IServiceCollection AddBookmarksServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IBookmarkService, BookmarkService>();
        services.AddScoped<IBookmarkFolderService, BookmarkFolderService>();
        services.AddScoped<IBookmarkImportExportService, BookmarkImportExportService>();
        services.AddScoped<IBookmarkPreviewService, BookmarkPreviewFetchService>();
        return services;
    }
}
