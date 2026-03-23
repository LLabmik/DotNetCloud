using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Import;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Contacts.Data.Services;

/// <summary>
/// Import provider that parses vCard data and creates contacts.
/// Supports dry-run mode for previewing imports without persistence.
/// </summary>
public sealed class ContactsImportProvider : IImportProvider
{
    private readonly IVCardService _vcardService;
    private readonly IContactService _contactService;
    private readonly ILogger<ContactsImportProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactsImportProvider"/> class.
    /// </summary>
    public ContactsImportProvider(
        IVCardService vcardService,
        IContactService contactService,
        ILogger<ContactsImportProvider> logger)
    {
        _vcardService = vcardService;
        _contactService = contactService;
        _logger = logger;
    }

    /// <inheritdoc />
    public ImportDataType DataType => ImportDataType.Contacts;

    /// <inheritdoc />
    public Task<ImportReport> PreviewAsync(
        ImportRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        return BuildReportAsync(request, caller, dryRun: true, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ImportReport> ExecuteAsync(
        ImportRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (request.DryRun)
        {
            return PreviewAsync(request, caller, cancellationToken);
        }

        return BuildReportAsync(request, caller, dryRun: false, cancellationToken);
    }

    private async Task<ImportReport> BuildReportAsync(
        ImportRequest request,
        CallerContext caller,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var items = new List<ImportItemResult>();

        if (string.IsNullOrWhiteSpace(request.Data))
        {
            return CreateReport(request, items, dryRun, startedAt);
        }

        var parsedContacts = ParseVCards(request.Data);

        for (var i = 0; i < parsedContacts.Count; i++)
        {
            var dto = parsedContacts[i];
            var displayName = dto.DisplayName;

            try
            {
                if (string.IsNullOrWhiteSpace(dto.DisplayName))
                {
                    items.Add(new ImportItemResult
                    {
                        Index = i,
                        DisplayName = $"(item {i + 1})",
                        Status = ImportItemStatus.Failed,
                        Message = "Missing required display name (FN property)."
                    });
                    continue;
                }

                if (dryRun)
                {
                    items.Add(new ImportItemResult
                    {
                        Index = i,
                        DisplayName = displayName,
                        Status = ImportItemStatus.Success,
                        Message = "Would be imported."
                    });
                }
                else
                {
                    var created = await _contactService.CreateContactAsync(dto, caller, cancellationToken);
                    items.Add(new ImportItemResult
                    {
                        Index = i,
                        DisplayName = displayName,
                        Status = ImportItemStatus.Success,
                        RecordId = created.Id
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to import contact at index {Index}: {DisplayName}", i, displayName);
                items.Add(new ImportItemResult
                {
                    Index = i,
                    DisplayName = displayName,
                    Status = ImportItemStatus.Failed,
                    Message = ex.Message
                });
            }
        }

        var report = CreateReport(request, items, dryRun, startedAt);
        _logger.LogInformation(
            "Contact import {Mode}: {Total} total, {Success} success, {Skipped} skipped, {Failed} failed for user {UserId}",
            dryRun ? "preview" : "execute",
            report.TotalItems, report.SuccessCount, report.SkippedCount, report.FailedCount,
            caller.UserId);

        return report;
    }

    /// <summary>
    /// Parses vCard text into contact creation DTOs.
    /// Extracted as internal for direct test access.
    /// </summary>
    internal static IReadOnlyList<CreateContactDto> ParseVCards(string text)
    {
        var results = new List<CreateContactDto>();
        var lines = text.Split('\n', StringSplitOptions.TrimEntries);

        string? displayName = null;
        string? firstName = null;
        string? lastName = null;
        string? middleName = null;
        string? prefix = null;
        string? suffix = null;
        string? org = null;
        string? dept = null;
        string? jobTitle = null;
        string? notes = null;
        string? websiteUrl = null;
        DateOnly? birthday = null;
        var emails = new List<ContactEmailDto>();
        var phones = new List<ContactPhoneDto>();
        var addresses = new List<ContactAddressDto>();
        bool inVCard = false;

        foreach (var line in lines)
        {
            if (line.Equals("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase))
            {
                inVCard = true;
                displayName = null; firstName = null; lastName = null; middleName = null;
                prefix = null; suffix = null; org = null; dept = null; jobTitle = null;
                notes = null; websiteUrl = null; birthday = null;
                emails = []; phones = []; addresses = [];
                continue;
            }

            if (line.Equals("END:VCARD", StringComparison.OrdinalIgnoreCase))
            {
                if (inVCard && displayName is not null)
                {
                    results.Add(new CreateContactDto
                    {
                        ContactType = ContactType.Person,
                        DisplayName = displayName,
                        FirstName = firstName,
                        LastName = lastName,
                        MiddleName = middleName,
                        Prefix = prefix,
                        Suffix = suffix,
                        Organization = org,
                        Department = dept,
                        JobTitle = jobTitle,
                        Notes = notes,
                        Birthday = birthday,
                        WebsiteUrl = websiteUrl,
                        Emails = emails,
                        PhoneNumbers = phones,
                        Addresses = addresses
                    });
                }
                else if (inVCard)
                {
                    // Add with empty display name — will be caught by validation
                    results.Add(new CreateContactDto
                    {
                        ContactType = ContactType.Person,
                        DisplayName = string.Empty,
                    });
                }
                inVCard = false;
                continue;
            }

            if (!inVCard) continue;

            if (line.StartsWith("FN:", StringComparison.OrdinalIgnoreCase))
            {
                displayName = UnescapeVCardValue(line[3..]);
            }
            else if (line.StartsWith("N:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("N;", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var parts = line[(colonIdx + 1)..].Split(';');
                    if (parts.Length > 0) lastName = NullIfEmpty(UnescapeVCardValue(parts[0]));
                    if (parts.Length > 1) firstName = NullIfEmpty(UnescapeVCardValue(parts[1]));
                    if (parts.Length > 2) middleName = NullIfEmpty(UnescapeVCardValue(parts[2]));
                    if (parts.Length > 3) prefix = NullIfEmpty(UnescapeVCardValue(parts[3]));
                    if (parts.Length > 4) suffix = NullIfEmpty(UnescapeVCardValue(parts[4]));
                }
            }
            else if (line.StartsWith("ORG:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("ORG;", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var parts = line[(colonIdx + 1)..].Split(';');
                    org = NullIfEmpty(UnescapeVCardValue(parts[0]));
                    if (parts.Length > 1) dept = NullIfEmpty(UnescapeVCardValue(parts[1]));
                }
            }
            else if (line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
            {
                jobTitle = UnescapeVCardValue(line[6..]);
            }
            else if (line.StartsWith("EMAIL", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var paramPart = line[..colonIdx].ToUpperInvariant();
                    var address = UnescapeVCardValue(line[(colonIdx + 1)..]);
                    var label = paramPart.Contains("WORK") ? "work" : paramPart.Contains("HOME") ? "home" : "other";
                    var isPrimary = paramPart.Contains("PREF");
                    emails.Add(new ContactEmailDto { Address = address, Label = label, IsPrimary = isPrimary });
                }
            }
            else if (line.StartsWith("TEL", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var paramPart = line[..colonIdx].ToUpperInvariant();
                    var number = UnescapeVCardValue(line[(colonIdx + 1)..]);
                    var label = paramPart.Contains("CELL") || paramPart.Contains("MOBILE") ? "mobile"
                        : paramPart.Contains("WORK") ? "work"
                        : paramPart.Contains("HOME") ? "home"
                        : paramPart.Contains("FAX") ? "fax"
                        : "other";
                    var isPrimary = paramPart.Contains("PREF");
                    phones.Add(new ContactPhoneDto { Number = number, Label = label, IsPrimary = isPrimary });
                }
            }
            else if (line.StartsWith("ADR", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var paramPart = line[..colonIdx].ToUpperInvariant();
                    var parts = line[(colonIdx + 1)..].Split(';');
                    var label = paramPart.Contains("WORK") ? "work" : paramPart.Contains("HOME") ? "home" : "other";
                    var isPrimary = paramPart.Contains("PREF");
                    addresses.Add(new ContactAddressDto
                    {
                        Label = label,
                        Street = parts.Length > 2 ? NullIfEmpty(UnescapeVCardValue(parts[2])) : null,
                        City = parts.Length > 3 ? NullIfEmpty(UnescapeVCardValue(parts[3])) : null,
                        Region = parts.Length > 4 ? NullIfEmpty(UnescapeVCardValue(parts[4])) : null,
                        PostalCode = parts.Length > 5 ? NullIfEmpty(UnescapeVCardValue(parts[5])) : null,
                        Country = parts.Length > 6 ? NullIfEmpty(UnescapeVCardValue(parts[6])) : null,
                        IsPrimary = isPrimary
                    });
                }
            }
            else if (line.StartsWith("BDAY:", StringComparison.OrdinalIgnoreCase))
            {
                if (DateOnly.TryParse(line[5..], out var bday))
                    birthday = bday;
            }
            else if (line.StartsWith("URL:", StringComparison.OrdinalIgnoreCase))
            {
                websiteUrl = UnescapeVCardValue(line[4..]);
            }
            else if (line.StartsWith("NOTE:", StringComparison.OrdinalIgnoreCase))
            {
                notes = UnescapeVCardValue(line[5..]);
            }
        }

        return results;
    }

    private static ImportReport CreateReport(
        ImportRequest request,
        IReadOnlyList<ImportItemResult> items,
        bool dryRun,
        DateTime startedAt)
    {
        return new ImportReport
        {
            IsDryRun = dryRun,
            DataType = ImportDataType.Contacts,
            Source = request.Source,
            TotalItems = items.Count,
            SuccessCount = items.Count(i => i.Status == ImportItemStatus.Success),
            SkippedCount = items.Count(i => i.Status == ImportItemStatus.Skipped),
            FailedCount = items.Count(i => i.Status == ImportItemStatus.Failed),
            ConflictCount = items.Count(i => i.Status == ImportItemStatus.Conflict),
            Items = items,
            ConflictStrategy = request.ConflictStrategy,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTime.UtcNow
        };
    }

    private static string UnescapeVCardValue(string value)
    {
        return value
            .Replace("\\n", "\n")
            .Replace("\\;", ";")
            .Replace("\\,", ",")
            .Replace("\\\\", "\\");
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
