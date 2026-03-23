using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Data.Services;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Calendar.Tests;

[TestClass]
public class CalendarImportProviderTests
{
    private CalendarDbContext _db = null!;
    private CalendarImportProvider _provider = null!;
    private CallerContext _caller = null!;
    private Mock<ICalendarEventService> _eventServiceMock = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CalendarDbContext(options);
        _eventServiceMock = new Mock<ICalendarEventService>();
        _provider = new CalendarImportProvider(
            _eventServiceMock.Object, NullLogger<CalendarImportProvider>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public void DataType_IsCalendarEvents()
    {
        Assert.AreEqual(ImportDataType.CalendarEvents, _provider.DataType);
    }

    [TestMethod]
    public async Task PreviewAsync_ValidICalendar_ReturnsDryRunReport()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.CalendarEvents,
            Data = TwoEventICalendar,
            DryRun = true,
            TargetContainerId = Guid.NewGuid()
        };

        var report = await _provider.PreviewAsync(request, _caller);

        Assert.IsTrue(report.IsDryRun);
        Assert.AreEqual(2, report.TotalItems);
        Assert.AreEqual(2, report.SuccessCount);
        Assert.AreEqual(0, report.FailedCount);
    }

    [TestMethod]
    public async Task PreviewAsync_DoesNotCallEventService()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.CalendarEvents,
            Data = TwoEventICalendar,
            DryRun = true,
            TargetContainerId = Guid.NewGuid()
        };

        await _provider.PreviewAsync(request, _caller);

        _eventServiceMock.Verify(
            s => s.CreateEventAsync(It.IsAny<CreateCalendarEventDto>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_ValidICalendar_CreatesEvents()
    {
        var calendarId = Guid.NewGuid();
        _eventServiceMock.Setup(s => s.CreateEventAsync(It.IsAny<CreateCalendarEventDto>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateCalendarEventDto dto, CallerContext _, CancellationToken _) => new CalendarEventDto
            {
                Id = Guid.NewGuid(),
                CalendarId = dto.CalendarId,
                CreatedByUserId = _caller.UserId,
                Title = dto.Title,
                StartUtc = dto.StartUtc,
                EndUtc = dto.EndUtc,
                IsAllDay = dto.IsAllDay,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var request = new ImportRequest
        {
            DataType = ImportDataType.CalendarEvents,
            Data = TwoEventICalendar,
            TargetContainerId = calendarId
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.IsFalse(report.IsDryRun);
        Assert.AreEqual(2, report.SuccessCount);
        _eventServiceMock.Verify(
            s => s.CreateEventAsync(It.Is<CreateCalendarEventDto>(d => d.CalendarId == calendarId), _caller, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task ExecuteAsync_DryRunFlag_DoesNotCreate()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.CalendarEvents,
            Data = TwoEventICalendar,
            DryRun = true,
            TargetContainerId = Guid.NewGuid()
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.IsTrue(report.IsDryRun);
        _eventServiceMock.Verify(
            s => s.CreateEventAsync(It.IsAny<CreateCalendarEventDto>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task PreviewAsync_EmptyData_ReturnsEmptyReport()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.CalendarEvents,
            Data = "",
            TargetContainerId = Guid.NewGuid()
        };

        var report = await _provider.PreviewAsync(request, _caller);

        Assert.AreEqual(0, report.TotalItems);
    }

    [TestMethod]
    public async Task PreviewAsync_MissingSummary_ReportsFailedItem()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.CalendarEvents,
            Data = "BEGIN:VCALENDAR\nBEGIN:VEVENT\nDTSTART:20260301T100000Z\nDTEND:20260301T110000Z\nEND:VEVENT\nEND:VCALENDAR",
            DryRun = true,
            TargetContainerId = Guid.NewGuid()
        };

        var report = await _provider.PreviewAsync(request, _caller);

        Assert.AreEqual(1, report.TotalItems);
        Assert.AreEqual(1, report.FailedCount);
        Assert.IsTrue(report.Items[0].Message!.Contains("title", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task PreviewAsync_NoTargetContainer_ReportsFailedItem()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.CalendarEvents,
            Data = "BEGIN:VCALENDAR\nBEGIN:VEVENT\nSUMMARY:Test Event\nDTSTART:20260301T100000Z\nDTEND:20260301T110000Z\nEND:VEVENT\nEND:VCALENDAR",
            DryRun = true
            // No TargetContainerId
        };

        var report = await _provider.PreviewAsync(request, _caller);

        Assert.AreEqual(1, report.TotalItems);
        Assert.AreEqual(1, report.FailedCount);
        Assert.IsTrue(report.Items[0].Message!.Contains("calendar ID", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ParseICalEvents_MultipleEvents_ParsesAll()
    {
        var parsed = CalendarImportProvider.ParseICalEvents(TwoEventICalendar);

        Assert.AreEqual(2, parsed.Count);
        Assert.AreEqual("Team Meeting", parsed[0].Title);
        Assert.AreEqual("Project Review", parsed[1].Title);
    }

    [TestMethod]
    public void ParseICalEvents_AllDayEvent_SetsIsAllDay()
    {
        var ical = "BEGIN:VCALENDAR\nBEGIN:VEVENT\nSUMMARY:Holiday\nDTSTART;VALUE=DATE:20260325\nDTEND;VALUE=DATE:20260326\nEND:VEVENT\nEND:VCALENDAR";
        var parsed = CalendarImportProvider.ParseICalEvents(ical);

        Assert.AreEqual(1, parsed.Count);
        Assert.IsTrue(parsed[0].IsAllDay);
    }

    [TestMethod]
    public void ParseICalEvents_WithDescription_ParsesDescription()
    {
        var ical = "BEGIN:VCALENDAR\nBEGIN:VEVENT\nSUMMARY:Test\nDESCRIPTION:Detailed description\nDTSTART:20260301T100000Z\nEND:VEVENT\nEND:VCALENDAR";
        var parsed = CalendarImportProvider.ParseICalEvents(ical);

        Assert.AreEqual("Detailed description", parsed[0].Description);
    }

    [TestMethod]
    public void ParseICalEvents_WithRecurrenceRule_ParsesRRule()
    {
        var ical = "BEGIN:VCALENDAR\nBEGIN:VEVENT\nSUMMARY:Weekly Standup\nDTSTART:20260301T090000Z\nRRULE:FREQ=WEEKLY;BYDAY=MO\nEND:VEVENT\nEND:VCALENDAR";
        var parsed = CalendarImportProvider.ParseICalEvents(ical);

        Assert.AreEqual("FREQ=WEEKLY;BYDAY=MO", parsed[0].RecurrenceRule);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReportContainsTimestamps()
    {
        var before = DateTime.UtcNow;
        var request = new ImportRequest
        {
            DataType = ImportDataType.CalendarEvents,
            Data = "",
            TargetContainerId = Guid.NewGuid()
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.IsTrue(report.StartedAtUtc >= before);
        Assert.IsTrue(report.CompletedAtUtc >= report.StartedAtUtc);
    }

    private const string TwoEventICalendar = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//Test//Test//EN
        BEGIN:VEVENT
        SUMMARY:Team Meeting
        DTSTART:20260301T100000Z
        DTEND:20260301T110000Z
        LOCATION:Conference Room A
        END:VEVENT
        BEGIN:VEVENT
        SUMMARY:Project Review
        DTSTART:20260302T140000Z
        DTEND:20260302T150000Z
        DESCRIPTION:Quarterly project review
        END:VEVENT
        END:VCALENDAR
        """;
}
