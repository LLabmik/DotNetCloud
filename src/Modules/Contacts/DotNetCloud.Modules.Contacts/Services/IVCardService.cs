using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Modules.Contacts.Services;

/// <summary>
/// vCard import/export operations.
/// </summary>
public interface IVCardService
{
    /// <summary>Exports a contact as vCard 3.0 text.</summary>
    Task<string> ExportVCardAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Imports contacts from vCard text (supports multiple vCards).</summary>
    Task<IReadOnlyList<Guid>> ImportVCardsAsync(string vCardText, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Exports all contacts for a user as a single vCard stream.</summary>
    Task<string> ExportAllVCardsAsync(CallerContext caller, CancellationToken cancellationToken = default);
}
