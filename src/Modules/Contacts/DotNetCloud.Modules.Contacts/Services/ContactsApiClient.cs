using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Contacts.Services;

/// <summary>
/// HTTP implementation of <see cref="IContactsApiClient"/>.
/// </summary>
public sealed class ContactsApiClient : IContactsApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ContactsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ContactDto>> ListContactsAsync(string? search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/contacts?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        return await ReadDataAsync<IReadOnlyList<ContactDto>>(url, cancellationToken) ?? [];
    }

    public Task<ContactDto?> GetContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        return ReadDataAsync<ContactDto>($"api/v1/contacts/{contactId}", cancellationToken);
    }

    public async Task<ContactDto?> CreateContactAsync(CreateContactDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/contacts", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<ContactDto>(response, cancellationToken);
    }

    public async Task<ContactDto?> UpdateContactAsync(Guid contactId, UpdateContactDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/contacts/{contactId}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<ContactDto>(response, cancellationToken);
    }

    public async Task DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/contacts/{contactId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ContactGroupDto>> ListGroupsAsync(CancellationToken cancellationToken = default)
    {
        return await ReadDataAsync<IReadOnlyList<ContactGroupDto>>("api/v1/contacts/groups", cancellationToken) ?? [];
    }

    public async Task<ContactRelatedEntitiesDto> GetRelatedAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        return await ReadDataAsync<ContactRelatedEntitiesDto>($"api/v1/contacts/{contactId}/related", cancellationToken)
            ?? new ContactRelatedEntitiesDto();
    }

    public async Task<IReadOnlyList<ContactShareResponse>> ListSharesAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        return await ReadDataAsync<IReadOnlyList<ContactShareResponse>>($"api/v1/contacts/{contactId}/shares", cancellationToken) ?? [];
    }

    public async Task<ContactShareResponse?> ShareContactAsync(Guid contactId, Guid? userId, Guid? teamId, string permission = "ReadOnly", CancellationToken cancellationToken = default)
    {
        var body = new { UserId = userId, TeamId = teamId, Permission = permission };
        var response = await _httpClient.PostAsJsonAsync($"api/v1/contacts/{contactId}/shares", body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<ContactShareResponse>(response, cancellationToken);
    }

    public async Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/contacts/shares/{shareId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<string?> GetAvatarUrlAsync(Guid contactId)
    {
        return Task.FromResult<string?>($"{_httpClient.BaseAddress}api/v1/contacts/{contactId}/avatar");
    }

    private async Task<T?> ReadDataAsync<T>(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<T>(response, cancellationToken);
    }

    private static async Task<T?> ReadDataAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);

        if (!TryGetPropertyIgnoreCase(document.RootElement, "data", out var dataElement))
        {
            return default;
        }

        if (dataElement.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(dataElement, "data", out var nestedData))
        {
            dataElement = nestedData;
        }

        return JsonSerializer.Deserialize<T>(dataElement.GetRawText(), JsonOptions);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
