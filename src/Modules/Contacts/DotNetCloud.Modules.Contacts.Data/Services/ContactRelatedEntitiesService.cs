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
            relatedEvents = await _calendarDb.EventAttendees
                .AsNoTracking()
                .Include(a => a.Event)
                .Where(a => a.Event != null && emailSet.Contains(a.Email.ToLower()))
                .Select(a => a.Event!)
                .Distinct()
                .OrderByDescending(e => e.StartUtc)
                .Select(e => new CalendarEventSummaryDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    StartUtc = e.StartUtc,
                    EndUtc = e.EndUtc
                })
                .Take(50)
                .ToListAsync(cancellationToken);
        }

        var relatedNotes = new List<NoteSummaryDto>();
        if (_notesDb is not null)
        {
            relatedNotes = await _notesDb.NoteLinks
                .AsNoTracking()
                .Include(l => l.Note)
                .Where(l => l.LinkType == NoteLinkType.Contact && l.TargetId == contactId && l.Note != null)
                .Select(l => l.Note!)
                .Distinct()
                .OrderByDescending(n => n.UpdatedAt)
                .Select(n => new NoteSummaryDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    UpdatedAt = n.UpdatedAt
                })
                .Take(50)
                .ToListAsync(cancellationToken);
        }

        return new ContactRelatedEntitiesDto
        {
            Events = relatedEvents,
            Notes = relatedNotes
        };
    }
}
