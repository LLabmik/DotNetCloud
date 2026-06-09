using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Contacts.Host.Protos;
using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Contacts gRPC client used by the Core Server.
/// </summary>
public sealed class ContactsGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "ContactsGrpc";

    /// <summary>
    /// The gRPC address of the Contacts module (e.g., "http://localhost:5002",
    /// "unix:///run/dotnetcloud/dotnetcloud-contacts.sock").
    /// </summary>
    public string ContactsModuleAddress { get; set; } = "http://localhost:5002";

    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="IContactsApiClient"/>.
/// Calls the Contacts module's gRPC service instead of its REST API.
/// </summary>
public sealed class ContactsGrpcApiClient : IContactsApiClient, IDisposable
{
    private readonly ContactsGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ContactsGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<ContactsService.ContactsServiceClient> _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="ContactsGrpcApiClient"/> class.</summary>
    public ContactsGrpcApiClient(
        IOptions<ContactsGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ContactsGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<ContactsService.ContactsServiceClient>(
            () => new ContactsService.ContactsServiceClient(_channel.Value));
    }

    private string GetUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId) ? userId : Guid.Empty.ToString();
    }

    // ─── Contact CRUD ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactDto>> ListContactsAsync(
        string? search, int skip, int take, CancellationToken cancellationToken = default)
        => (await SafeCallAsync(async () =>
        {
            var request = new ListContactsRequest
            {
                UserId = GetUserId(),
                Search = search ?? string.Empty,
                Skip = skip,
                Take = take
            };
            var response = await _client.Value.ListContactsAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? [] : response.Contacts.Select(c => ToContactDto(c)!).Where(c => c is not null).Select(c => c!).ToList();
        }, "ListContacts", []))!;

    /// <inheritdoc />
    public async Task<ContactDto?> GetContactAsync(Guid contactId, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new GetContactRequest { ContactId = contactId.ToString(), UserId = GetUserId() };
            var response = await _client.Value.GetContactAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToContactDto(response.Contact) : null;
        }, "GetContact");

    /// <inheritdoc />
    public async Task<ContactDto?> CreateContactAsync(CreateContactDto dto, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = ToCreateRequest(dto);
            var response = await _client.Value.CreateContactAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToContactDto(response.Contact) : null;
        }, "CreateContact");

    /// <inheritdoc />
    public async Task<ContactDto?> UpdateContactAsync(Guid contactId, UpdateContactDto dto, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = ToUpdateRequest(contactId, dto);
            var response = await _client.Value.UpdateContactAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return response.Success ? ToContactDto(response.Contact) : null;
        }, "UpdateContact");

    /// <inheritdoc />
    public async Task DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new DeleteContactRequest { ContactId = contactId.ToString(), UserId = GetUserId() };
            await _client.Value.DeleteContactAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
        }, "DeleteContact");

    // ─── Groups ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactGroupDto>> ListGroupsAsync(CancellationToken cancellationToken = default)
        => (await SafeCallListAsync(async () =>
        {
            var request = new ListGroupsRequest { UserId = GetUserId() };
            var response = await _client.Value.ListGroupsAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? (IReadOnlyList<ContactGroupDto>)[] : response.Groups.Select(g => new ContactGroupDto
            {
                Id = Guid.Parse(g.Id),
                OwnerId = Guid.Parse(g.OwnerId),
                Name = g.Name,
                MemberCount = g.MemberCount,
                CreatedAt = DateTime.MinValue,
                UpdatedAt = DateTime.MinValue
            }).ToList();
        }, "ListGroups", Array.Empty<ContactGroupDto>()))!;

    /// <inheritdoc />
    public async Task<ContactRelatedEntitiesDto> GetRelatedAsync(Guid contactId, CancellationToken cancellationToken = default)
        => (await SafeCallAsync(async () =>
        {
            var request = new GetContactRelatedRequest { ContactId = contactId.ToString(), UserId = GetUserId() };
            var response = await _client.Value.GetContactRelatedAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            if (!response.Success)
                return new ContactRelatedEntitiesDto();
            return new ContactRelatedEntitiesDto
            {
                Events = response.RelatedItems.Where(i => i.EntityType == "CalendarEvent").Select(i => new CalendarEventSummaryDto
                { Id = Guid.Parse(i.Id), Title = i.Title, StartUtc = DateTime.Parse(i.CreatedAt), EndUtc = DateTime.Parse(i.CreatedAt) }).ToList(),
                Notes = response.RelatedItems.Where(i => i.EntityType == "Note").Select(i => new NoteSummaryDto
                { Id = Guid.Parse(i.Id), Title = i.Title, UpdatedAt = DateTime.Parse(i.CreatedAt) }).ToList()
            };
        }, "GetRelated", new ContactRelatedEntitiesDto()))!;

    // ─── Sharing ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactShareResponse>> ListSharesAsync(Guid contactId, CancellationToken cancellationToken = default)
        => (await SafeCallListAsync(async () =>
        {
            var request = new ListContactSharesRequest { ContactId = contactId.ToString(), UserId = GetUserId() };
            var response = await _client.Value.ListContactSharesAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            return !response.Success ? (IReadOnlyList<ContactShareResponse>)[] : response.Shares.Select(s => new ContactShareResponse
            {
                Id = Guid.Parse(s.Id),
                ContactId = Guid.Parse(s.ContactId),
                SharedByUserId = Guid.Parse(s.SharedByUserId),
                SharedWithUserId = string.IsNullOrEmpty(s.SharedWithUserId) ? null : Guid.Parse(s.SharedWithUserId),
                SharedWithTeamId = string.IsNullOrEmpty(s.SharedWithTeamId) ? null : Guid.Parse(s.SharedWithTeamId),
                Permission = s.Permission,
                CreatedAt = DateTime.Parse(s.CreatedAt)
            }).ToList();
        }, "ListShares", Array.Empty<ContactShareResponse>()))!;

    /// <inheritdoc />
    public async Task<ContactShareResponse?> ShareContactAsync(Guid contactId, Guid? userId, Guid? teamId, string permission = "ReadOnly", CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new ShareContactRequest
            {
                ContactId = contactId.ToString(),
                UserId = GetUserId(),
                TargetUserId = userId?.ToString() ?? string.Empty,
                TeamId = teamId?.ToString() ?? string.Empty,
                Permission = permission
            };
            var response = await _client.Value.ShareContactAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
            if (!response.Success || response.Share is null)
                return null;
            var s = response.Share;
            return new ContactShareResponse
            {
                Id = Guid.Parse(s.Id),
                ContactId = Guid.Parse(s.ContactId),
                SharedByUserId = Guid.Parse(s.SharedByUserId),
                SharedWithUserId = string.IsNullOrEmpty(s.SharedWithUserId) ? null : Guid.Parse(s.SharedWithUserId),
                SharedWithTeamId = string.IsNullOrEmpty(s.SharedWithTeamId) ? null : Guid.Parse(s.SharedWithTeamId),
                Permission = s.Permission,
                CreatedAt = DateTime.Parse(s.CreatedAt)
            };
        }, "ShareContact");

    /// <inheritdoc />
    public async Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken = default)
        => await SafeCallAsync(async () =>
        {
            var request = new RevokeContactShareRequest { ShareId = shareId.ToString(), UserId = GetUserId() };
            await _client.Value.RevokeContactShareAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
        }, "RevokeShare");

    /// <inheritdoc />
    public Task<string?> GetAvatarUrlAsync(Guid contactId)
    {
        // Avatar URLs are served by the Contacts module's REST API.
        // The gRPC client doesn't serve binary content, so construct the URL from the configured address.
        var baseAddress = _options.ContactsModuleAddress
            .Replace("unix://", "http://")
            .Replace("net.pipe://", "http://");
        return Task.FromResult<string?>($"{baseAddress}/api/v1/contacts/{contactId}/avatar");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<T> SafeCallListAsync<T>(Func<Task<T>> call, string operation, T fallback) where T : class
    {
        try
        { return await call(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        { _logger.LogWarning("Contacts {Op} gRPC unavailable", operation); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        { _logger.LogWarning("Contacts {Op} gRPC timed out", operation); }
        catch (Exception ex)
        { _logger.LogError(ex, "Contacts {Op} unexpected error", operation); }
        return fallback;
    }

    private async Task<T?> SafeCallAsync<T>(Func<Task<T?>> call, string operation, T? fallback = default)
    {
        try
        { return await call(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        { _logger.LogWarning("Contacts {Op} gRPC unavailable", operation); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        { _logger.LogWarning("Contacts {Op} gRPC timed out", operation); }
        catch (Exception ex)
        { _logger.LogError(ex, "Contacts {Op} unexpected error", operation); }
        return fallback;
    }

    private async Task SafeCallAsync(Func<Task> call, string operation)
    {
        try
        { await call(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        { _logger.LogWarning("Contacts {Op} gRPC unavailable", operation); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        { _logger.LogWarning("Contacts {Op} gRPC timed out", operation); }
        catch (Exception ex)
        { _logger.LogError(ex, "Contacts {Op} unexpected error", operation); }
    }

    private CallOptions DeadlineHeaders(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.Add(_options.Timeout);
        return new CallOptions(deadline: deadline, cancellationToken: ct);
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.contacts");
        _logger.LogInformation("ContactsGrpcApiClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    private CreateContactRequest ToCreateRequest(CreateContactDto dto) => new()
    {
        UserId = GetUserId(),
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
        Emails = { dto.Emails.Select(e => new ContactEmailMessage { Address = e.Address, Label = e.Label, IsPrimary = e.IsPrimary }) },
        Phones = { dto.PhoneNumbers.Select(p => new ContactPhoneMessage { Number = p.Number, Label = p.Label, IsPrimary = p.IsPrimary }) },
        Addresses = { dto.Addresses.Select(a => new ContactAddressMessage { Label = a.Label, Street = a.Street ?? string.Empty, City = a.City ?? string.Empty, Region = a.Region ?? string.Empty, PostalCode = a.PostalCode ?? string.Empty, Country = a.Country ?? string.Empty, IsPrimary = a.IsPrimary }) }
    };

    private UpdateContactRequest ToUpdateRequest(Guid contactId, UpdateContactDto dto) => new()
    {
        ContactId = contactId.ToString(),
        UserId = GetUserId(),
        DisplayName = dto.DisplayName ?? string.Empty,
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
        WebsiteUrl = dto.WebsiteUrl ?? string.Empty
    };

    private static ContactDto? ToContactDto(ContactMessage? c)
    {
        if (c is null)
            return null;
        try
        {
            return new ContactDto
            {
                Id = Guid.Parse(c.Id),
                OwnerId = Guid.Parse(c.OwnerId),
                ContactType = Enum.TryParse<ContactType>(c.ContactType, out var ct) ? ct : ContactType.Person,
                DisplayName = c.DisplayName,
                FirstName = string.IsNullOrEmpty(c.FirstName) ? null : c.FirstName,
                LastName = string.IsNullOrEmpty(c.LastName) ? null : c.LastName,
                MiddleName = string.IsNullOrEmpty(c.MiddleName) ? null : c.MiddleName,
                Prefix = string.IsNullOrEmpty(c.Prefix) ? null : c.Prefix,
                Suffix = string.IsNullOrEmpty(c.Suffix) ? null : c.Suffix,
                Organization = string.IsNullOrEmpty(c.Organization) ? null : c.Organization,
                Department = string.IsNullOrEmpty(c.Department) ? null : c.Department,
                JobTitle = string.IsNullOrEmpty(c.JobTitle) ? null : c.JobTitle,
                Notes = string.IsNullOrEmpty(c.Notes) ? null : c.Notes,
                Birthday = string.IsNullOrEmpty(c.Birthday) ? null : DateOnly.Parse(c.Birthday),
                WebsiteUrl = string.IsNullOrEmpty(c.WebsiteUrl) ? null : c.WebsiteUrl,
                AvatarUrl = string.IsNullOrEmpty(c.AvatarUrl) ? null : c.AvatarUrl,
                ETag = string.IsNullOrEmpty(c.Etag) ? null : c.Etag,
                CreatedAt = DateTime.Parse(c.CreatedAt),
                UpdatedAt = DateTime.Parse(c.UpdatedAt),
                Emails = c.Emails.Select(e => new ContactEmailDto { Address = e.Address, Label = e.Label, IsPrimary = e.IsPrimary }).ToList(),
                PhoneNumbers = c.Phones.Select(p => new ContactPhoneDto { Number = p.Number, Label = p.Label, IsPrimary = p.IsPrimary }).ToList(),
                Addresses = c.Addresses.Select(a => new ContactAddressDto { Label = a.Label, Street = string.IsNullOrEmpty(a.Street) ? null : a.Street, City = string.IsNullOrEmpty(a.City) ? null : a.City, Region = string.IsNullOrEmpty(a.Region) ? null : a.Region, PostalCode = string.IsNullOrEmpty(a.PostalCode) ? null : a.PostalCode, Country = string.IsNullOrEmpty(a.Country) ? null : a.Country, IsPrimary = a.IsPrimary }).ToList(),
                CustomFields = new Dictionary<string, string>(c.CustomFields)
            };
        }
        catch (Exception ex)
        {
            // Log and return null on mapping failures (e.g., bad GUID format from module)
            System.Diagnostics.Debug.WriteLine($"ContactsGrpcApiClient.ToContactDto mapping error: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_channel.IsValueCreated)
        {
            try
            { _channel.Value.Dispose(); }
            catch { /* ignore */ }
        }
    }
}
