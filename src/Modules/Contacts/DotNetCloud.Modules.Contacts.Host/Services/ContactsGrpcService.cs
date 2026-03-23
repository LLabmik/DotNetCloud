using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Contacts.Host.Protos;
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
    private readonly ILogger<ContactsGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactsGrpcService"/> class.
    /// </summary>
    public ContactsGrpcService(
        IContactService contactService,
        IVCardService vcardService,
        ILogger<ContactsGrpcService> logger)
    {
        _contactService = contactService;
        _vcardService = vcardService;
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
                dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

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
            contactId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

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
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new ListContactsResponse { Success = false, ErrorMessage = "Invalid user ID format." };
        }

        var take = request.Take > 0 ? request.Take : 50;

        var results = await _contactService.ListContactsAsync(
            new CallerContext(userId, ["user"], CallerType.User),
            NullIfEmpty(request.Search),
            request.Skip,
            take,
            context.CancellationToken);

        var response = new ListContactsResponse { Success = true };
        response.Contacts.AddRange(results.Select(ToContactMessage));
        return response;
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
                contactId, dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

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
                contactId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

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
                contactId, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

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
                request.VcardText, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

            var response = new ImportVCardsResponse { Success = true };
            response.CreatedContactIds.AddRange(ids.Select(id => id.ToString()));
            return response;
        }
        catch (Exception ex) when (ex is ArgumentException or Core.Errors.ValidationException)
        {
            return new ImportVCardsResponse { Success = false, ErrorMessage = ex.Message };
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
}
