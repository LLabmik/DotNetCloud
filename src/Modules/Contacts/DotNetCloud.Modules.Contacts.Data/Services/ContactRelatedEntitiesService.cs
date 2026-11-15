using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Notes.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Contacts.Data.Services;

/// <summary>
/// Default implementation for contact reverse cross-module lookups.
/// </summary>
public sealed class ContactRelatedEntitiesService : IContactRelatedEntitiesService
{
    private readonly ContactsDbContext _contactsDb;
    private readonly CalendarDbContext? _calendarDb;
    private readonly NotesDbContext? _notesDb;

    public ContactRelatedEntitiesService(
        ContactsDbContext contactsDb,
        CalendarDbContext? calendarDb = null,
        NotesDbContext? notesDb = null)
    {
        _contactsDb = contactsDb;
        _calendarDb = calendarDb;
        _notesDb = notesDb;
    }

    public async Task<ContactRelatedEntitiesDto> GetRelatedAsync(Guid contactId, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var contact = await _contactsDb.Contacts
            .AsNoTracking()
            .Include(c => c.Emails)
            .FirstOrDefaultAsync(c => c.Id == contactId && c.OwnerId == ownerId, cancellationToken);

        if (contact is null)
        {
            return new ContactRelatedEntitiesDto();
        }

        var emailSet = contact.Emails
            .Select(e => e.Address.Trim().ToLowerInvariant())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .ToHashSet();

        var relatedEvents = new List<CalendarEventSummaryDto>();
        if (_calendarDb is not null && emailSet.Count > 0)
        {
            // Project the four values the contact card shows *before* de-duplicating. A contact can
            // match more than one attendee row on the same event (one row per invited address), so a
            // DISTINCT over the matched events is needed — but over the whole CalendarEvent entity it
            // asks the database to compare every column, and SQL Server refuses to compare `text`
            // columns ("The text data type cannot be selected as DISTINCT because it is not
            // comparable"), which turned opening a contact into a 500.
            relatedEvents = await _calendarDb.EventAttendees
                .AsNoTracking()
                .Where(a => emailSet.Contains(a.Email.ToLower()))
                .Select(a => new
                {
                    a.Event!.Id,
                    a.Event!.Title,
                    a.Event!.StartUtc,
                    a.Event!.EndUtc
                })
                .Distinct()
                .OrderByDescending(e => e.StartUtc)
                .Take(50)
                .Select(e => new CalendarEventSummaryDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    StartUtc = e.StartUtc,
                    EndUtc = e.EndUtc
                })
                .ToListAsync(cancellationToken);
        }

        var relatedNotes = new List<NoteSummaryDto>();
        if (_notesDb is not null)
        {
            // Same shape as the events query: `core.Notes.Content` is a `text` column on SQL Server, so
            // de-duplicating the note entity could never be evaluated. Three scalar columns can.
            relatedNotes = await _notesDb.NoteLinks
                .AsNoTracking()
                .Where(l => l.LinkType == NoteLinkType.Contact && l.TargetId == contactId)
                .Select(l => new
                {
                    l.Note!.Id,
                    l.Note!.Title,
                    l.Note!.UpdatedAt
                })
                .Distinct()
                .OrderByDescending(n => n.UpdatedAt)
                .Take(50)
                .Select(n => new NoteSummaryDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    UpdatedAt = n.UpdatedAt
                })
                .ToListAsync(cancellationToken);
        }

        return new ContactRelatedEntitiesDto
        {
            Events = relatedEvents,
            Notes = relatedNotes
        };
    }
}
