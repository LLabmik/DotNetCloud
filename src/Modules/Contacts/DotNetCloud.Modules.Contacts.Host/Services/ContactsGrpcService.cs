using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Contacts.Host.Protos;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Contacts.Services;
using Grpc.Core;

namespace DotNetCloud.Modules.Contacts.Host.Services;

/// <summary>
/// gRPC service implementation for the Contacts module.
/// Exposes contact operations over gRPC for the core server to invoke.
/// </summary>
public sealed class ContactsGrpcService : ContactsService.ContactsServiceBase
{
    private readonly IContactService _contactService;
    private readonly IVCardService _vcardService;
    private readonly IContactDirectory _contactDirectory;
    private readonly IContactGroupService _groupService;
    private readonly IContactShareService _shareService;
    private readonly IContactRelatedEntitiesService _relatedService;
    private readonly ILogger<ContactsGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactsGrpcService"/> class.
    /// </summary>
    public ContactsGrpcService(
        IContactService contactService,
        IVCardService vcardService,
        IContactDirectory contactDirectory,
        IContactGroupService groupService,
        IContactShareService shareService,
        IContactRelatedEntitiesService relatedService,
        ILogger<ContactsGrpcService> logger)
    {
        _contactService = contactService;
        _vcardService = vcardService;
        _contactDirectory = contactDirectory;
        _groupService = groupService;
        _shareService = shareService;
        _relatedService = relatedService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ContactResponse> CreateContact(
        CreateContactRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new ContactResponse { Success = false, ErrorMessage = "Invalid user ID format." };
        }

        if (!Enum.TryParse<ContactType>(request.ContactType, true, out var contactType))
        {
            return new ContactResponse { Success = false, ErrorMessage = "Invalid contact type." };
        }

        var dto = new CreateContactDto
        {
            ContactType = contactType,
            DisplayName = request.DisplayName,
            FirstName = NullIfEmpty(request.FirstName),
            LastName = NullIfEmpty(request.LastName),
            MiddleName = NullIfEmpty(request.MiddleName),
            Prefix = NullIfEmpty(request.Prefix),
            Suffix = NullIfEmpty(request.Suffix),
            Organization = NullIfEmpty(request.Organization),
            Department = NullIfEmpty(request.Department),
            JobTitle = NullIfEmpty(request.JobTitle),
            Notes = NullIfEmpty(request.Notes),
            Birthday = ParseDate(request.Birthday),
            WebsiteUrl = NullIfEmpty(request.WebsiteUrl),
            Emails = request.Emails.Select(e => new ContactEmailDto
            {
                Address = e.Address,
                Label = e.Label,
                IsPrimary = e.IsPrimary
            }).ToList(),
            PhoneNumbers = request.Phones.Select(p => new ContactPhoneDto
            {
                Number = p.Number,
                Label = p.Label,
                IsPrimary = p.IsPrimary
            }).ToList(),
            Addresses = request.Addresses.Select(a => new ContactAddressDto
            {
                Label = a.Label,
                Street = NullIfEmpty(a.Street),
                City = NullIfEmpty(a.City),
                Region = NullIfEmpty(a.Region),
                PostalCode = NullIfEmpty(a.PostalCode),
                Country = NullIfEmpty(a.Country),
                IsPrimary = a.IsPrimary
            }).ToList(),
            CustomFields = new Dictionary<string, string>(request.CustomFields)
        };

        try
        {
            var result = await _contactService.CreateContactAsync(
                dto, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

            return new ContactResponse { Success = true, Contact = ToContactMessage(result) };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new ContactResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ContactResponse> GetContact(
        GetContactRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ContactId, out var contactId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new ContactResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        var result = await _contactService.GetContactAsync(
            contactId, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

        if (result is null)
        {
            return new ContactResponse { Success = false, ErrorMessage = "Contact not found." };
        }

        return new ContactResponse { Success = true, Contact = ToContactMessage(result) };
    }

    /// <inheritdoc />
    public override async Task<ListContactsResponse> ListContacts(
        ListContactsRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                return new ListContactsResponse { Success = false, ErrorMessage = "Invalid user ID format." };
            }

            var take = request.Take > 0 ? request.Take : 50;

            var results = await _contactService.ListContactsAsync(
                new CallerContext(userId, Array.Empty<string>(), CallerType.User),
                NullIfEmpty(request.Search),
                request.Skip,
                take,
                context.CancellationToken);

            var response = new ListContactsResponse { Success = true };
            response.Contacts.AddRange(results.Select(ToContactMessage));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListContacts gRPC handler failed");
            return new ListContactsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ContactResponse> UpdateContact(
        UpdateContactRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ContactId, out var contactId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new ContactResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        var dto = new UpdateContactDto
        {
            DisplayName = NullIfEmpty(request.DisplayName),
            FirstName = NullIfEmpty(request.FirstName),
            LastName = NullIfEmpty(request.LastName),
            MiddleName = NullIfEmpty(request.MiddleName),
            Prefix = NullIfEmpty(request.Prefix),
            Suffix = NullIfEmpty(request.Suffix),
            Organization = NullIfEmpty(request.Organization),
            Department = NullIfEmpty(request.Department),
            JobTitle = NullIfEmpty(request.JobTitle),
            Notes = NullIfEmpty(request.Notes),
            Birthday = ParseDate(request.Birthday),
            WebsiteUrl = NullIfEmpty(request.WebsiteUrl)
        };

        try
        {
            var result = await _contactService.UpdateContactAsync(
                contactId, dto, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

            return new ContactResponse { Success = true, Contact = ToContactMessage(result) };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new ContactResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<DeleteContactResponse> DeleteContact(
        DeleteContactRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ContactId, out var contactId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new DeleteContactResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        try
        {
            await _contactService.DeleteContactAsync(
                contactId, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

            return new DeleteContactResponse { Success = true };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new DeleteContactResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ExportVCardResponse> ExportVCard(
        ExportVCardRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ContactId, out var contactId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new ExportVCardResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        try
        {
            var vcard = await _vcardService.ExportVCardAsync(
                contactId, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

            return new ExportVCardResponse { Success = true, VcardText = vcard };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new ExportVCardResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ImportVCardsResponse> ImportVCards(
        ImportVCardsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new ImportVCardsResponse { Success = false, ErrorMessage = "Invalid user ID format." };
        }

        try
        {
            var ids = await _vcardService.ImportVCardsAsync(
                request.VcardText, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

            var response = new ImportVCardsResponse { Success = true };
            response.CreatedContactIds.AddRange(ids.Select(id => id.ToString()));
            return response;
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new ImportVCardsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<SearchContactsResponse> SearchContacts(
        SearchContactsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new SearchContactsResponse { Success = false, ErrorMessage = "Invalid user ID format." };
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new SearchContactsResponse { Success = true };
        }

        var maxResults = request.MaxResults > 0 ? request.MaxResults : 10;

        try
        {
            var results = await _contactDirectory.SearchContactsWithEmailsAsync(
                userId, request.Query, maxResults, context.CancellationToken);

            var response = new SearchContactsResponse { Success = true };
            response.Results.AddRange(results.Select(r => new ContactSearchResultMessage
            {
                ContactId = r.ContactId.ToString(),
                DisplayName = r.DisplayName,
                Emails =
                {
                    r.Emails.Select(e => new ContactEmailMessage
                    {
                        Address = e.Address,
                        Label = e.Label,
                        IsPrimary = false
                    })
                }
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching contacts for user {UserId} with query '{Query}'", userId, request.Query);
            return new SearchContactsResponse { Success = false, ErrorMessage = "An error occurred while searching contacts." };
        }
    }

    private static ContactMessage ToContactMessage(ContactDto dto)
    {
        var msg = new ContactMessage
        {
            Id = dto.Id.ToString(),
            OwnerId = dto.OwnerId.ToString(),
            ContactType = dto.ContactType.ToString(),
            DisplayName = dto.DisplayName,
            FirstName = dto.FirstName ?? string.Empty,
            LastName = dto.LastName ?? string.Empty,
            MiddleName = dto.MiddleName ?? string.Empty,
            Prefix = dto.Prefix ?? string.Empty,
            Suffix = dto.Suffix ?? string.Empty,
            Organization = dto.Organization ?? string.Empty,
            Department = dto.Department ?? string.Empty,
            JobTitle = dto.JobTitle ?? string.Empty,
            Notes = dto.Notes ?? string.Empty,
            Birthday = dto.Birthday?.ToString("yyyy-MM-dd") ?? string.Empty,
            WebsiteUrl = dto.WebsiteUrl ?? string.Empty,
            AvatarUrl = dto.AvatarUrl ?? string.Empty,
            Etag = dto.ETag ?? string.Empty,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O")
        };

        msg.Emails.AddRange(dto.Emails.Select(e => new ContactEmailMessage
        {
            Address = e.Address,
            Label = e.Label,
            IsPrimary = e.IsPrimary
        }));

        msg.Phones.AddRange(dto.PhoneNumbers.Select(p => new ContactPhoneMessage
        {
            Number = p.Number,
            Label = p.Label,
            IsPrimary = p.IsPrimary
        }));

        msg.Addresses.AddRange(dto.Addresses.Select(a => new ContactAddressMessage
        {
            Label = a.Label,
            Street = a.Street ?? string.Empty,
            City = a.City ?? string.Empty,
            Region = a.Region ?? string.Empty,
            PostalCode = a.PostalCode ?? string.Empty,
            Country = a.Country ?? string.Empty,
            IsPrimary = a.IsPrimary
        }));

        foreach (var (key, value) in dto.CustomFields)
        {
            msg.CustomFields.Add(key, value);
        }

        return msg;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static DateOnly? ParseDate(string? value) =>
        !string.IsNullOrEmpty(value) && DateOnly.TryParse(value, out var date)
            ? date
            : null;

    /// <inheritdoc />
    public override async Task GetSearchableDocuments(
        GetSearchableDocumentsRequest request,
        IServerStreamWriter<SearchableDocument> responseStream,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return;

        var contacts = await _contactService.ListContactsAsync(
            CallerContext.CreateSystemContext(),
            search: null, skip: 0, take: int.MaxValue,
            context.CancellationToken);

        foreach (var contact in contacts)
        {
            var doc = MapContactToSearchableDocument(contact);
            await responseStream.WriteAsync(doc, context.CancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task<SearchableDocumentResponse> GetSearchableDocument(
        GetSearchableDocumentRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EntityId, out var entityId))
            return new SearchableDocumentResponse { Found = false };

        var contact = await _contactService.GetContactAsync(
            entityId,
            new CallerContext(Guid.Empty, ["system"], CallerType.System),
            context.CancellationToken);

        if (contact is null)
            return new SearchableDocumentResponse { Found = false };

        return new SearchableDocumentResponse
        {
            Found = true,
            Document = MapContactToSearchableDocument(contact)
        };
    }

    private static SearchableDocument MapContactToSearchableDocument(ContactDto dto)
    {
        var contentParts = new List<string>();
        if (!string.IsNullOrEmpty(dto.FirstName))
            contentParts.Add(dto.FirstName);
        if (!string.IsNullOrEmpty(dto.LastName))
            contentParts.Add(dto.LastName);
        if (!string.IsNullOrEmpty(dto.Organization))
            contentParts.Add(dto.Organization);
        if (!string.IsNullOrEmpty(dto.Department))
            contentParts.Add(dto.Department);
        if (!string.IsNullOrEmpty(dto.JobTitle))
            contentParts.Add(dto.JobTitle);
        if (!string.IsNullOrEmpty(dto.Notes))
            contentParts.Add(dto.Notes);
        foreach (var email in dto.Emails)
            contentParts.Add(email.Address);
        foreach (var phone in dto.PhoneNumbers)
            contentParts.Add(phone.Number);

        var doc = new SearchableDocument
        {
            ModuleId = "contacts",
            EntityId = dto.Id.ToString(),
            EntityType = "Contact",
            Title = dto.DisplayName,
            Content = string.Join(" ", contentParts),
            Summary = dto.Organization ?? dto.JobTitle ?? string.Empty,
            OwnerId = dto.OwnerId.ToString(),
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt.ToString("O")
        };

        doc.Metadata["ContactType"] = dto.ContactType.ToString();

        return doc;
    }

    // ─── Groups ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public override async Task<ListGroupsResponse> ListGroups(
        ListGroupsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new ListGroupsResponse { Success = false, ErrorMessage = "Invalid user ID format." };

        var groups = await _groupService.ListGroupsAsync(
            new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

        var response = new ListGroupsResponse { Success = true };
        response.Groups.AddRange(groups.Select(g => new GroupMessage
        {
            Id = g.Id.ToString(),
            OwnerId = g.OwnerId.ToString(),
            Name = g.Name,
            MemberCount = g.MemberCount,
            CreatedAt = g.CreatedAt.ToString("O")
        }));
        return response;
    }

    /// <inheritdoc />
    public override async Task<GroupResponse> GetGroup(
        GetGroupRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.GroupId, out var groupId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new GroupResponse { Success = false, ErrorMessage = "Invalid ID format." };

        var group = await _groupService.GetGroupAsync(
            groupId, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

        return group is null
            ? new GroupResponse { Success = false, ErrorMessage = "Group not found." }
            : new GroupResponse
            {
                Success = true,
                Group = new GroupMessage
                {
                    Id = group.Id.ToString(),
                    OwnerId = group.OwnerId.ToString(),
                    Name = group.Name,
                    MemberCount = group.MemberCount,
                    CreatedAt = group.CreatedAt.ToString("O")
                }
            };
    }

    /// <inheritdoc />
    public override async Task<GroupResponse> CreateGroup(
        CreateGroupRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new GroupResponse { Success = false, ErrorMessage = "Invalid user ID format." };

        try
        {
            var group = await _groupService.CreateGroupAsync(
                request.Name, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

            return new GroupResponse
            {
                Success = true,
                Group = new GroupMessage
                {
                    Id = group.Id.ToString(),
                    OwnerId = group.OwnerId.ToString(),
                    Name = group.Name,
                    MemberCount = group.MemberCount,
                    CreatedAt = group.CreatedAt.ToString("O")
                }
            };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new GroupResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GroupResponse> RenameGroup(
        RenameGroupRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.GroupId, out var groupId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new GroupResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            var group = await _groupService.RenameGroupAsync(
                groupId, request.NewName, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

            return new GroupResponse
            {
                Success = true,
                Group = new GroupMessage
                {
                    Id = group.Id.ToString(),
                    OwnerId = group.OwnerId.ToString(),
                    Name = group.Name,
                    MemberCount = group.MemberCount,
                    CreatedAt = group.CreatedAt.ToString("O")
                }
            };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new GroupResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<DeleteGroupResponse> DeleteGroup(
        DeleteGroupRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.GroupId, out var groupId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new DeleteGroupResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            await _groupService.DeleteGroupAsync(
                groupId, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);
            return new DeleteGroupResponse { Success = true };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new DeleteGroupResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ─── Group Members ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override async Task<AddContactToGroupResponse> AddContactToGroup(
        AddContactToGroupRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.GroupId, out var groupId) ||
            !Guid.TryParse(request.ContactId, out var contactId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new AddContactToGroupResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            await _groupService.AddContactToGroupAsync(
                groupId, contactId, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);
            return new AddContactToGroupResponse { Success = true };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new AddContactToGroupResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<RemoveContactFromGroupResponse> RemoveContactFromGroup(
        RemoveContactFromGroupRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.GroupId, out var groupId) ||
            !Guid.TryParse(request.ContactId, out var contactId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new RemoveContactFromGroupResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            await _groupService.RemoveContactFromGroupAsync(
                groupId, contactId, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);
            return new RemoveContactFromGroupResponse { Success = true };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new RemoveContactFromGroupResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListGroupMembersResponse> ListGroupMembers(
        ListGroupMembersRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.GroupId, out var groupId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new ListGroupMembersResponse { Success = false, ErrorMessage = "Invalid ID format." };

        var members = await _groupService.ListGroupMembersAsync(
            groupId, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

        var response = new ListGroupMembersResponse { Success = true };
        response.Members.AddRange(members.Select(ToContactMessage));
        return response;
    }

    // ─── Related Entities ────────────────────────────────────────────────────

    /// <inheritdoc />
    public override async Task<ContactRelatedResponse> GetContactRelated(
        GetContactRelatedRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ContactId, out var contactId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new ContactRelatedResponse { Success = false, ErrorMessage = "Invalid ID format." };

        var related = await _relatedService.GetRelatedAsync(contactId, userId, context.CancellationToken);

        var response = new ContactRelatedResponse { Success = true };

        foreach (var evt in related.Events)
        {
            response.RelatedItems.Add(new ContactRelatedItem
            {
                Id = evt.Id.ToString(),
                EntityType = "CalendarEvent",
                Title = evt.Title,
                Description = string.Empty,
                Url = string.Empty,
                CreatedAt = evt.StartUtc.ToString("O")
            });
        }

        foreach (var note in related.Notes)
        {
            response.RelatedItems.Add(new ContactRelatedItem
            {
                Id = note.Id.ToString(),
                EntityType = "Note",
                Title = note.Title,
                Description = string.Empty,
                Url = string.Empty,
                CreatedAt = note.UpdatedAt.ToString("O")
            });
        }

        return response;
    }

    // ─── Sharing ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public override async Task<ListContactSharesResponse> ListContactShares(
        ListContactSharesRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ContactId, out var contactId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new ListContactSharesResponse { Success = false, ErrorMessage = "Invalid ID format." };

        var shares = await _shareService.ListSharesAsync(
            contactId, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

        var response = new ListContactSharesResponse { Success = true };
        response.Shares.AddRange(shares.Select(s => new ContactShareMessage
        {
            Id = s.Id.ToString(),
            ContactId = s.ContactId.ToString(),
            SharedByUserId = s.SharedByUserId.ToString(),
            SharedWithUserId = s.SharedWithUserId?.ToString() ?? string.Empty,
            SharedWithTeamId = s.SharedWithTeamId?.ToString() ?? string.Empty,
            Permission = s.Permission.ToString(),
            CreatedAt = s.CreatedAt.ToString("O")
        }));
        return response;
    }

    /// <inheritdoc />
    public override async Task<ShareContactResponse> ShareContact(
        ShareContactRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ContactId, out var contactId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new ShareContactResponse { Success = false, ErrorMessage = "Invalid ID format." };

        var targetUserId = Guid.TryParse(request.TargetUserId, out var tuid) ? tuid : (Guid?)null;
        var teamId = Guid.TryParse(request.TeamId, out var tid) ? tid : (Guid?)null;

        if (!Enum.TryParse<ContactSharePermission>(request.Permission, true, out var permission))
            permission = ContactSharePermission.ReadOnly;

        try
        {
            var share = await _shareService.ShareContactAsync(
                contactId, targetUserId, teamId, permission,
                new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);

            return new ShareContactResponse
            {
                Success = true,
                Share = new ContactShareMessage
                {
                    Id = share.Id.ToString(),
                    ContactId = share.ContactId.ToString(),
                    SharedByUserId = share.SharedByUserId.ToString(),
                    SharedWithUserId = share.SharedWithUserId?.ToString() ?? string.Empty,
                    SharedWithTeamId = share.SharedWithTeamId?.ToString() ?? string.Empty,
                    Permission = share.Permission.ToString(),
                    CreatedAt = share.CreatedAt.ToString("O")
                }
            };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new ShareContactResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<RevokeContactShareResponse> RevokeContactShare(
        RevokeContactShareRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ShareId, out var shareId) ||
            !Guid.TryParse(request.UserId, out var userId))
            return new RevokeContactShareResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            await _shareService.RemoveShareAsync(
                shareId, new CallerContext(userId, Array.Empty<string>(), CallerType.User), context.CancellationToken);
            return new RevokeContactShareResponse { Success = true };
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new RevokeContactShareResponse { Success = false, ErrorMessage = ex.Message };
        }
    }
}
