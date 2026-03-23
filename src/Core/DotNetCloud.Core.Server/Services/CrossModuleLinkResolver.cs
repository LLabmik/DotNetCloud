using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Resolves cross-module links by delegating to the appropriate module directory capability.
/// </summary>
internal sealed class CrossModuleLinkResolver : ICrossModuleLinkResolver
{
    private readonly IContactDirectory? _contactDirectory;
    private readonly ICalendarDirectory? _calendarDirectory;
    private readonly INoteDirectory? _noteDirectory;
    private readonly ILogger<CrossModuleLinkResolver> _logger;

    public CrossModuleLinkResolver(
        ILogger<CrossModuleLinkResolver> logger,
        IContactDirectory? contactDirectory = null,
        ICalendarDirectory? calendarDirectory = null,
        INoteDirectory? noteDirectory = null)
    {
        _logger = logger;
        _contactDirectory = contactDirectory;
        _calendarDirectory = calendarDirectory;
        _noteDirectory = noteDirectory;
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
            _logger.LogWarning(ex, "Failed to resolve cross-module link: {LinkType} {TargetId}", linkType, targetId);
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

        // Batch resolve contacts
        if (contactIds.Count > 0 && _contactDirectory is not null)
        {
            try
            {
                var names = await _contactDirectory.GetContactDisplayNamesAsync(
                    contactIds.Select(c => c.Id), cancellationToken);
                foreach (var (index, id) in contactIds)
                {
                    resolved[index] = names.TryGetValue(id, out var name)
                        ? new CrossModuleLinkDto
                        {
                            LinkType = CrossModuleLinkType.Contact,
                            TargetId = id,
                            DisplayLabel = name,
                            Href = $"/apps/contacts/{id}",
                            IsResolved = true
                        }
                        : CreateUnresolvedLink(CrossModuleLinkType.Contact, id, "Contact");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Batch contact resolution failed");
                foreach (var (index, id) in contactIds)
                    resolved[index] = CreateUnresolvedLink(CrossModuleLinkType.Contact, id, "Contact");
            }
        }
        else
        {
            foreach (var (index, id) in contactIds)
                resolved[index] = CreateUnresolvedLink(CrossModuleLinkType.Contact, id, "Contact");
        }

        // Batch resolve notes
        if (noteIds.Count > 0 && _noteDirectory is not null)
        {
            try
            {
                var titles = await _noteDirectory.GetNoteTitlesAsync(
                    noteIds.Select(n => n.Id), cancellationToken);
                foreach (var (index, id) in noteIds)
                {
                    resolved[index] = titles.TryGetValue(id, out var title)
                        ? new CrossModuleLinkDto
                        {
                            LinkType = CrossModuleLinkType.Note,
                            TargetId = id,
                            DisplayLabel = title,
                            Href = $"/apps/notes/{id}",
                            IsResolved = true
                        }
                        : CreateUnresolvedLink(CrossModuleLinkType.Note, id, "Note");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Batch note resolution failed");
                foreach (var (index, id) in noteIds)
                    resolved[index] = CreateUnresolvedLink(CrossModuleLinkType.Note, id, "Note");
            }
        }
        else
        {
            foreach (var (index, id) in noteIds)
                resolved[index] = CreateUnresolvedLink(CrossModuleLinkType.Note, id, "Note");
        }

        // Resolve calendar events individually (no batch API on ICalendarDirectory)
        foreach (var (index, id) in calendarIds)
        {
            resolved[index] = await ResolveCalendarEventAsync(id, cancellationToken);
        }

        return resolved;
    }

    private async Task<CrossModuleLinkDto> ResolveContactAsync(Guid contactId, CancellationToken cancellationToken)
    {
        if (_contactDirectory is null)
            return CreateUnresolvedLink(CrossModuleLinkType.Contact, contactId, "Contact");

        var name = await _contactDirectory.GetContactDisplayNameAsync(contactId, cancellationToken);
        return name is not null
            ? new CrossModuleLinkDto
            {
                LinkType = CrossModuleLinkType.Contact,
                TargetId = contactId,
                DisplayLabel = name,
                Href = $"/apps/contacts/{contactId}",
                IsResolved = true
            }
            : CreateUnresolvedLink(CrossModuleLinkType.Contact, contactId, "Contact");
    }

    private async Task<CrossModuleLinkDto> ResolveCalendarEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        if (_calendarDirectory is null)
            return CreateUnresolvedLink(CrossModuleLinkType.CalendarEvent, eventId, "Event");

        var summary = await _calendarDirectory.GetEventSummaryAsync(eventId, cancellationToken);
        return summary is not null
            ? new CrossModuleLinkDto
            {
                LinkType = CrossModuleLinkType.CalendarEvent,
                TargetId = eventId,
                DisplayLabel = summary.Title,
                Href = $"/apps/calendar/event/{eventId}",
                IsResolved = true
            }
            : CreateUnresolvedLink(CrossModuleLinkType.CalendarEvent, eventId, "Event");
    }

    private async Task<CrossModuleLinkDto> ResolveNoteAsync(Guid noteId, CancellationToken cancellationToken)
    {
        if (_noteDirectory is null)
            return CreateUnresolvedLink(CrossModuleLinkType.Note, noteId, "Note");

        var title = await _noteDirectory.GetNoteTitleAsync(noteId, cancellationToken);
        return title is not null
            ? new CrossModuleLinkDto
            {
                LinkType = CrossModuleLinkType.Note,
                TargetId = noteId,
                DisplayLabel = title,
                Href = $"/apps/notes/{noteId}",
                IsResolved = true
            }
            : CreateUnresolvedLink(CrossModuleLinkType.Note, noteId, "Note");
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
