using DotNetCloud.Core;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Calendar.Services;
using DotNetCloud.Modules.Contacts.Services;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Resolves cross-module links by delegating to gRPC module API clients.
/// </summary>
internal sealed class CrossModuleLinkResolver : ICrossModuleLinkResolver
{
    private readonly IContactsApiClient _contactsClient;
    private readonly ICalendarApiClient _calendarClient;
    private readonly INotesApiClient _notesClient;
    private readonly ILogger<CrossModuleLinkResolver> _logger;

    public CrossModuleLinkResolver(
        ILogger<CrossModuleLinkResolver> logger,
        IContactsApiClient contactsClient,
        ICalendarApiClient calendarClient,
        INotesApiClient notesClient)
    {
        _logger = logger;
        _contactsClient = contactsClient;
        _calendarClient = calendarClient;
        _notesClient = notesClient;
    }

    /// <inheritdoc />
    public async Task<CrossModuleLinkDto> ResolveAsync(
        CrossModuleLinkType linkType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return linkType switch
            {
                CrossModuleLinkType.Contact => await ResolveContactAsync(targetId, cancellationToken),
                CrossModuleLinkType.CalendarEvent => await ResolveCalendarEventAsync(targetId, cancellationToken),
                CrossModuleLinkType.Note => await ResolveNoteAsync(targetId, cancellationToken),
                CrossModuleLinkType.File => CreateUnresolvedLink(linkType, targetId, "File"),
                _ => CreateUnresolvedLink(linkType, targetId, "Unknown")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve cross-module link: {LinkType} {TargetId}", linkType, LogSanitizer.Sanitize(targetId.ToString()));
            return CreateUnresolvedLink(linkType, targetId, linkType.ToString());
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CrossModuleLinkDto>> ResolveBatchAsync(
        IReadOnlyList<CrossModuleLinkRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CrossModuleLinkDto>(requests.Count);

        // Group by type for efficient batch resolution
        var contactIds = new List<(int Index, Guid Id)>();
        var calendarIds = new List<(int Index, Guid Id)>();
        var noteIds = new List<(int Index, Guid Id)>();
        var resolved = new CrossModuleLinkDto[requests.Count];

        for (var i = 0; i < requests.Count; i++)
        {
            var req = requests[i];
            switch (req.LinkType)
            {
                case CrossModuleLinkType.Contact:
                    contactIds.Add((i, req.TargetId));
                    break;
                case CrossModuleLinkType.CalendarEvent:
                    calendarIds.Add((i, req.TargetId));
                    break;
                case CrossModuleLinkType.Note:
                    noteIds.Add((i, req.TargetId));
                    break;
                default:
                    resolved[i] = CreateUnresolvedLink(req.LinkType, req.TargetId, req.LinkType.ToString());
                    break;
            }
        }

        // Batch resolve contacts — call GetContactAsync for each ID via gRPC
        if (contactIds.Count > 0)
        {
            try
            {
                foreach (var (index, id) in contactIds)
                {
                    var contact = await _contactsClient.GetContactAsync(id, cancellationToken);
                    resolved[index] = contact is not null
                        ? new CrossModuleLinkDto
                        {
                            LinkType = CrossModuleLinkType.Contact,
                            TargetId = id,
                            DisplayLabel = contact.DisplayName,
                            Href = $"/apps/contacts/{id}",
                            IsResolved = true
                        }
                        : CreateUnresolvedLink(CrossModuleLinkType.Contact, id, "Contact");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Batch contact resolution failed via gRPC");
                foreach (var (index, id) in contactIds)
                    resolved[index] = CreateUnresolvedLink(CrossModuleLinkType.Contact, id, "Contact");
            }
        }
        else
        {
            foreach (var (index, id) in contactIds)
                resolved[index] = CreateUnresolvedLink(CrossModuleLinkType.Contact, id, "Contact");
        }

        // Batch resolve notes — call GetNoteAsync for each ID via gRPC
        if (noteIds.Count > 0)
        {
            try
            {
                foreach (var (index, id) in noteIds)
                {
                    var note = await _notesClient.GetNoteAsync(id, cancellationToken);
                    resolved[index] = note is not null
                        ? new CrossModuleLinkDto
                        {
                            LinkType = CrossModuleLinkType.Note,
                            TargetId = id,
                            DisplayLabel = note.Title,
                            Href = $"/apps/notes/{id}",
                            IsResolved = true
                        }
                        : CreateUnresolvedLink(CrossModuleLinkType.Note, id, "Note");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Batch note resolution failed via gRPC");
                foreach (var (index, id) in noteIds)
                    resolved[index] = CreateUnresolvedLink(CrossModuleLinkType.Note, id, "Note");
            }
        }
        else
        {
            foreach (var (index, id) in noteIds)
                resolved[index] = CreateUnresolvedLink(CrossModuleLinkType.Note, id, "Note");
        }

        // Resolve calendar events individually via gRPC
        foreach (var (index, id) in calendarIds)
        {
            resolved[index] = await ResolveCalendarEventAsync(id, cancellationToken);
        }

        return resolved;
    }

    private async Task<CrossModuleLinkDto> ResolveContactAsync(Guid contactId, CancellationToken cancellationToken)
    {
        try
        {
            var contact = await _contactsClient.GetContactAsync(contactId, cancellationToken);
            return contact is not null
                ? new CrossModuleLinkDto
                {
                    LinkType = CrossModuleLinkType.Contact,
                    TargetId = contactId,
                    DisplayLabel = contact.DisplayName,
                    Href = $"/apps/contacts/{contactId}",
                    IsResolved = true
                }
                : CreateUnresolvedLink(CrossModuleLinkType.Contact, contactId, "Contact");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "gRPC contact resolution failed for {ContactId}", contactId);
            return CreateUnresolvedLink(CrossModuleLinkType.Contact, contactId, "Contact");
        }
    }

    private async Task<CrossModuleLinkDto> ResolveCalendarEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        try
        {
            var evt = await _calendarClient.GetEventAsync(eventId, cancellationToken);
            return evt is not null
                ? new CrossModuleLinkDto
                {
                    LinkType = CrossModuleLinkType.CalendarEvent,
                    TargetId = eventId,
                    DisplayLabel = evt.Title,
                    Href = $"/apps/calendar/event/{eventId}",
                    IsResolved = true
                }
                : CreateUnresolvedLink(CrossModuleLinkType.CalendarEvent, eventId, "Event");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "gRPC calendar event resolution failed for {EventId}", eventId);
            return CreateUnresolvedLink(CrossModuleLinkType.CalendarEvent, eventId, "Event");
        }
    }

    private async Task<CrossModuleLinkDto> ResolveNoteAsync(Guid noteId, CancellationToken cancellationToken)
    {
        try
        {
            var note = await _notesClient.GetNoteAsync(noteId, cancellationToken);
            return note is not null
                ? new CrossModuleLinkDto
                {
                    LinkType = CrossModuleLinkType.Note,
                    TargetId = noteId,
                    DisplayLabel = note.Title,
                    Href = $"/apps/notes/{noteId}",
                    IsResolved = true
                }
                : CreateUnresolvedLink(CrossModuleLinkType.Note, noteId, "Note");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "gRPC note resolution failed for {NoteId}", noteId);
            return CreateUnresolvedLink(CrossModuleLinkType.Note, noteId, "Note");
        }
    }

    private static CrossModuleLinkDto CreateUnresolvedLink(CrossModuleLinkType linkType, Guid targetId, string fallbackLabel)
    {
        return new CrossModuleLinkDto
        {
            LinkType = linkType,
            TargetId = targetId,
            DisplayLabel = $"[Deleted {fallbackLabel}]",
            IsResolved = false
        };
    }
}
