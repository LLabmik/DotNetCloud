using DotNetCloud.Core.SharedWithMe;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.SharedWithMe;

/// <summary>
/// Adapts the Contacts module's "shared with me" REST query (via the in-process HTTP module
/// client <see cref="IContactsApiClient"/>) into an <see cref="ISharedWithMeProvider"/> so Files
/// can surface shared contacts as virtual deep-link entries under
/// <c>_DotNetCloud/SharedWithMe/Contacts</c>.
/// </summary>
/// <remarks>
/// Contacts is process-isolated; Core.Server talks to its host through the module HTTP client
/// (the same channel the ContactsPage UI uses, which forwards the caller's auth cookie). A fresh
/// DI scope is created per call. Module failures degrade gracefully to an empty list rather than
/// throwing into the Files listing.
/// </remarks>
public sealed class ContactsSharedWithMeProvider : ISharedWithMeProvider
{
    /// <summary>Stable module id for Contacts.</summary>
    public const string ContactsModuleId = "contacts";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContactsSharedWithMeProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactsSharedWithMeProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a DI scope per call.</param>
    /// <param name="logger">Logger for graceful module-failure diagnostics.</param>
    public ContactsSharedWithMeProvider(IServiceScopeFactory scopeFactory, ILogger<ContactsSharedWithMeProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ModuleId => ContactsModuleId;

    /// <inheritdoc />
    public string DisplayName => "Contacts";

    /// <inheritdoc />
    public string IconName => "person";

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default)
        => (await ListAsync(userId, cancellationToken)).Count;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedWithMeModuleItem>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IContactsApiClient>();

        IReadOnlyList<ContactSharedItem> items;
        try
        {
            items = await client.ListSharedWithMeAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Contacts shared-with-me lookup failed for {UserId}; returning empty", userId);
            return [];
        }

        return MapItems(userId, items);
    }

    /// <summary>
    /// Maps the contacts shared with <paramref name="userId"/> to shared-with-me items. Contacts
    /// the user shared themselves (e.g. to a team they belong to) are excluded so only genuine
    /// inbound shares are surfaced.
    /// </summary>
    /// <param name="userId">The recipient user id.</param>
    /// <param name="items">Shared contacts visible to the caller (user + team, unexpired).</param>
    internal static IReadOnlyList<SharedWithMeModuleItem> MapItems(Guid userId, IEnumerable<ContactSharedItem> items)
        => items
            .Where(item => item.SharedByUserId != userId && !string.IsNullOrWhiteSpace(item.DisplayName))
            .Select(item => new SharedWithMeModuleItem(
                ModuleId: ContactsModuleId,
                DisplayName: "Contacts",
                EntityId: item.ContactId,
                EntityType: "Contact",
                Title: item.DisplayName,
                Subtitle: "Contact",
                DeepLink: $"/apps/contacts?contactId={item.ContactId}",
                IconName: "person",
                UpdatedAt: item.UpdatedAt == default ? item.CreatedAt : item.UpdatedAt))
            .ToList();
}
