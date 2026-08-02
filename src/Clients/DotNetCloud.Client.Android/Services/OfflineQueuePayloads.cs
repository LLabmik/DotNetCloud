using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Services;

/// <summary>Payload for a queued chat message send.</summary>
/// <param name="ChannelId">Target channel.</param>
/// <param name="Content">Message body.</param>
public sealed record OfflineChatMessagePayload(Guid ChannelId, string Content);

/// <summary>Payload for a queued note create.</summary>
/// <param name="Dto">Create payload to replay.</param>
public sealed record OfflineNoteCreatePayload(CreateNoteDto Dto);

/// <summary>Payload for a queued note update.</summary>
/// <param name="NoteId">Target note.</param>
/// <param name="Dto">Update payload to replay.</param>
public sealed record OfflineNoteUpdatePayload(Guid NoteId, UpdateNoteDto Dto);

/// <summary>Payload for a queued note delete.</summary>
/// <param name="NoteId">Target note.</param>
public sealed record OfflineNoteDeletePayload(Guid NoteId);

/// <summary>Payload for a queued calendar event create.</summary>
/// <param name="Dto">Create payload to replay.</param>
public sealed record OfflineCalendarEventCreatePayload(CreateCalendarEventDto Dto);

/// <summary>Payload for a queued calendar event update.</summary>
/// <param name="EventId">Target event.</param>
/// <param name="Dto">Update payload to replay.</param>
public sealed record OfflineCalendarEventUpdatePayload(Guid EventId, UpdateCalendarEventDto Dto);

/// <summary>Payload for a queued calendar event delete.</summary>
/// <param name="EventId">Target event.</param>
public sealed record OfflineCalendarEventDeletePayload(Guid EventId);
