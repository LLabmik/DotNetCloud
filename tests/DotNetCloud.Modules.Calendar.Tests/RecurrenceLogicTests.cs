using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Unit tests for recurrence rule generation, parsing, and BYDAY handling
/// in the CalendarPage EventEditorModel logic.
/// These tests validate the RRULE builder and parser helpers without UI rendering.
/// </summary>
[TestClass]
public class RecurrenceLogicTests
{
    // ─── BuildRrule ─────────────────────────────────────────────────

    [TestMethod]
    public void BuildRrule_None_ReturnsNull()
    {
        var result = BuildRrule(recurrenceType: "None");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void BuildRrule_Empty_ReturnsNull()
    {
        var result = BuildRrule(recurrenceType: "");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void BuildRrule_Daily_ReturnsCorrectFreq()
    {
        var result = BuildRrule(recurrenceType: "Daily");
        Assert.AreEqual("FREQ=DAILY", result);
    }

    [TestMethod]
    public void BuildRrule_Weekly_ReturnsCorrectFreq()
    {
        var result = BuildRrule(recurrenceType: "Weekly");
        Assert.AreEqual("FREQ=WEEKLY", result);
    }

    [TestMethod]
    public void BuildRrule_Biweekly_ReturnsInterval2()
    {
        var result = BuildRrule(recurrenceType: "Biweekly");
        Assert.AreEqual("FREQ=WEEKLY;INTERVAL=2", result);
    }

    [TestMethod]
    public void BuildRrule_Monthly_ReturnsCorrectFreq()
    {
        var result = BuildRrule(recurrenceType: "Monthly");
        Assert.AreEqual("FREQ=MONTHLY", result);
    }

    [TestMethod]
    public void BuildRrule_Yearly_ReturnsCorrectFreq()
    {
        var result = BuildRrule(recurrenceType: "Yearly");
        Assert.AreEqual("FREQ=YEARLY", result);
    }

    [TestMethod]
    public void BuildRrule_Weekly_WithByDay()
    {
        var result = BuildRrule(recurrenceType: "Weekly", mon: true, wed: true, fri: true);
        Assert.AreEqual("FREQ=WEEKLY;BYDAY=MO,WE,FR", result);
    }

    [TestMethod]
    public void BuildRrule_Weekly_AllDaysSelected_NoByDay()
    {
        // All 7 days selected → no BYDAY (redundant)
        var result = BuildRrule(recurrenceType: "Weekly", mon: true, tue: true, wed: true,
            thu: true, fri: true, sat: true, sun: true);
        Assert.AreEqual("FREQ=WEEKLY", result);
    }

    [TestMethod]
    public void BuildRrule_Biweekly_WithByDay()
    {
        var result = BuildRrule(recurrenceType: "Biweekly", tue: true, thu: true);
        Assert.AreEqual("FREQ=WEEKLY;INTERVAL=2;BYDAY=TU,TH", result);
    }

    [TestMethod]
    public void BuildRrule_Monthly_WithByDayPosition_FirstMonday()
    {
        var result = BuildRrule(recurrenceType: "Monthly", mon: true, monthlyPosition: 1);
        Assert.AreEqual("FREQ=MONTHLY;BYDAY=1MO", result);
    }

    [TestMethod]
    public void BuildRrule_Monthly_WithByDayPosition_LastFriday()
    {
        var result = BuildRrule(recurrenceType: "Monthly", fri: true, monthlyPosition: -1);
        Assert.AreEqual("FREQ=MONTHLY;BYDAY=-1FR", result);
    }

    [TestMethod]
    public void BuildRrule_Monthly_WithByDayPosition_SecondTuesdayThursday()
    {
        var result = BuildRrule(recurrenceType: "Monthly", tue: true, thu: true, monthlyPosition: 2);
        Assert.AreEqual("FREQ=MONTHLY;BYDAY=2TU,2TH", result);
    }

    [TestMethod]
    public void BuildRrule_WithUntil()
    {
        var until = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var result = BuildRrule(recurrenceType: "Weekly", mon: true, until: until);
        Assert.AreEqual("FREQ=WEEKLY;BYDAY=MO;UNTIL=20261231T000000Z", result);
    }

    [TestMethod]
    public void BuildRrule_WithUntil_NonUtcDate()
    {
        // Local date should be converted to UTC
        var until = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Local);
        var result = BuildRrule(recurrenceType: "Daily", until: until);
        Assert.IsTrue(result!.Contains("UNTIL="));
        Assert.IsTrue(result.Contains("20260615"));
    }

    // ─── ParseRecurrenceType ────────────────────────────────────────

    [TestMethod]
    public void ParseRecurrenceType_Null_ReturnsNone()
    {
        Assert.AreEqual("None", ParseRecurrenceType(null));
    }

    [TestMethod]
    public void ParseRecurrenceType_Empty_ReturnsNone()
    {
        Assert.AreEqual("None", ParseRecurrenceType(""));
    }

    [TestMethod]
    public void ParseRecurrenceType_Daily()
    {
        Assert.AreEqual("Daily", ParseRecurrenceType("FREQ=DAILY"));
    }

    [TestMethod]
    public void ParseRecurrenceType_Weekly()
    {
        Assert.AreEqual("Weekly", ParseRecurrenceType("FREQ=WEEKLY;BYDAY=MO,WE"));
    }

    [TestMethod]
    public void ParseRecurrenceType_Biweekly()
    {
        Assert.AreEqual("Biweekly", ParseRecurrenceType("FREQ=WEEKLY;INTERVAL=2;BYDAY=TU"));
    }

    [TestMethod]
    public void ParseRecurrenceType_Monthly()
    {
        Assert.AreEqual("Monthly", ParseRecurrenceType("FREQ=MONTHLY;BYDAY=1MO"));
    }

    [TestMethod]
    public void ParseRecurrenceType_Yearly()
    {
        Assert.AreEqual("Yearly", ParseRecurrenceType("FREQ=YEARLY"));
    }

    [TestMethod]
    public void ParseRecurrenceType_Unknown_ReturnsNone()
    {
        Assert.AreEqual("None", ParseRecurrenceType("FREQ=HOURLY"));
    }

    // ─── HasByDay ───────────────────────────────────────────────────

    [TestMethod]
    public void HasByDay_Monday_True()
    {
        Assert.IsTrue(HasByDay("FREQ=WEEKLY;BYDAY=MO,WE", "MO"));
    }

    [TestMethod]
    public void HasByDay_Friday_False()
    {
        Assert.IsFalse(HasByDay("FREQ=WEEKLY;BYDAY=MO,WE", "FR"));
    }

    [TestMethod]
    public void HasByDay_NullRrule_False()
    {
        Assert.IsFalse(HasByDay(null, "MO"));
    }

    [TestMethod]
    public void HasByDay_WithPosition()
    {
        Assert.IsTrue(HasByDay("FREQ=MONTHLY;BYDAY=2TU,2TH", "TU"));
        Assert.IsTrue(HasByDay("FREQ=MONTHLY;BYDAY=2TU,2TH", "TH"));
        Assert.IsFalse(HasByDay("FREQ=MONTHLY;BYDAY=2TU,2TH", "MO"));
    }

    [TestMethod]
    public void HasByDay_LastFriday()
    {
        Assert.IsTrue(HasByDay("FREQ=MONTHLY;BYDAY=-1FR", "FR"));
        Assert.IsFalse(HasByDay("FREQ=MONTHLY;BYDAY=-1FR", "MO"));
    }

    // ─── ParseMonthlyByDayPosition ──────────────────────────────────

    [TestMethod]
    public void ParseMonthlyByDayPosition_First()
    {
        Assert.AreEqual(1, ParseMonthlyByDayPosition("FREQ=MONTHLY;BYDAY=1MO"));
    }

    [TestMethod]
    public void ParseMonthlyByDayPosition_Last()
    {
        Assert.AreEqual(-1, ParseMonthlyByDayPosition("FREQ=MONTHLY;BYDAY=-1FR,SU"));
    }

    [TestMethod]
    public void ParseMonthlyByDayPosition_NoByDay_ReturnsNull()
    {
        Assert.IsNull(ParseMonthlyByDayPosition("FREQ=MONTHLY"));
    }

    [TestMethod]
    public void ParseMonthlyByDayPosition_WeeklyNoPosition_ReturnsNull()
    {
        Assert.IsNull(ParseMonthlyByDayPosition("FREQ=WEEKLY;BYDAY=MO,WE"));
    }

    [TestMethod]
    public void ParseMonthlyByDayPosition_Null_ReturnsNull()
    {
        Assert.IsNull(ParseMonthlyByDayPosition(null));
    }

    // ─── EventEditorModel Round-Trip ────────────────────────────────

    [TestMethod]
    public void FromDto_RoundTrip_SimpleRecurrence()
    {
        var now = DateTime.UtcNow;
        var dto = new CalendarEventDto
        {
            Id = Guid.NewGuid(),
            CalendarId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            Title = "Test",
            StartUtc = now,
            EndUtc = now.AddHours(1),
            CreatedAt = now,
            UpdatedAt = now,
            RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO,WE,FR"
        };

        var model = TestEventEditorModel.From(dto);
        Assert.AreEqual("Weekly", model.RecurrenceType);
        Assert.IsTrue(model.RecurOnMon);
        Assert.IsTrue(model.RecurOnWed);
        Assert.IsTrue(model.RecurOnFri);
        Assert.IsFalse(model.RecurOnTue);

        // Round-trip: ToCreateDto should produce equivalent RRULE
        var createDto = model.ToCreateDto();
        Assert.IsTrue(createDto.RecurrenceRule!.Contains("FREQ=WEEKLY"));
        Assert.IsTrue(createDto.RecurrenceRule.Contains("MO"));
    }

    [TestMethod]
    public void FromDto_RoundTrip_MonthlyByDay()
    {
        var now = DateTime.UtcNow;
        var dto = new CalendarEventDto
        {
            Id = Guid.NewGuid(),
            CalendarId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            Title = "Monthly Meeting",
            StartUtc = now,
            EndUtc = now.AddHours(1),
            CreatedAt = now,
            UpdatedAt = now,
            RecurrenceRule = "FREQ=MONTHLY;BYDAY=2TU"
        };

        var model = TestEventEditorModel.From(dto);
        Assert.AreEqual("Monthly", model.RecurrenceType);
        Assert.AreEqual(2, model.MonthlyByDayPosition);
        Assert.IsTrue(model.RecurOnTue);

        // Round-trip
        var createDto = model.ToCreateDto();
        Assert.AreEqual("FREQ=MONTHLY;BYDAY=2TU", createDto.RecurrenceRule);
    }

    [TestMethod]
    public void FromDto_RoundTrip_AllDayEvent()
    {
        var now = DateTime.UtcNow;
        var dto = new CalendarEventDto
        {
            Id = Guid.NewGuid(),
            CalendarId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            Title = "All Day Event",
            StartUtc = now.Date,
            EndUtc = now.Date.AddDays(1),
            CreatedAt = now,
            UpdatedAt = now,
            IsAllDay = true,
            Color = "#ef4444",
            Url = "https://meet.example.com"
        };

        var model = TestEventEditorModel.From(dto);
        Assert.IsTrue(model.IsAllDay);
        Assert.AreEqual("#ef4444", model.Color);
        Assert.AreEqual("https://meet.example.com", model.Url);

        var createDto = model.ToCreateDto();
        Assert.IsTrue(createDto.IsAllDay);
        Assert.AreEqual("#ef4444", createDto.Color);
    }

    // ─── Helpers (mirrors EventEditorModel static methods) ─────────

    private static string? BuildRrule(
        string recurrenceType,
        bool mon = false, bool tue = false, bool wed = false, bool thu = false,
        bool fri = false, bool sat = false, bool sun = false,
        int? monthlyPosition = null,
        DateTime? until = null)
    {
        if (string.IsNullOrEmpty(recurrenceType) || recurrenceType == "None")
            return null;

        var freq = recurrenceType switch
        {
            "Daily" => "DAILY",
            "Weekly" => "WEEKLY",
            "Biweekly" => "WEEKLY",
            "Monthly" => "MONTHLY",
            "Yearly" => "YEARLY",
            _ => recurrenceType.ToUpperInvariant()
        };

        var rule = $"FREQ={freq}";

        if (recurrenceType == "Biweekly")
            rule += ";INTERVAL=2";

        var days = new List<string>();
        if (mon) days.Add("MO");
        if (tue) days.Add("TU");
        if (wed) days.Add("WE");
        if (thu) days.Add("TH");
        if (fri) days.Add("FR");
        if (sat) days.Add("SA");
        if (sun) days.Add("SU");

        if (days.Count > 0 && days.Count < 7)
        {
            if (recurrenceType == "Monthly" && monthlyPosition.HasValue)
            {
                var pos = monthlyPosition.Value;
                var posStr = pos == -1 ? "-1" : pos.ToString();
                rule += $";BYDAY={posStr}{string.Join($",{posStr}", days)}";
            }
            else
            {
                rule += $";BYDAY={string.Join(",", days)}";
            }
        }

        if (until.HasValue)
            rule += $";UNTIL={until.Value.ToUniversalTime():yyyyMMddTHHmmssZ}";

        return rule;
    }

    private static string ParseRecurrenceType(string? rrule)
    {
        if (string.IsNullOrWhiteSpace(rrule))
            return "None";

        var upper = rrule.ToUpperInvariant();
        var hasInterval2 = upper.Contains("INTERVAL=2") || upper.Contains("INTERVAL=2;");

        if (upper.StartsWith("FREQ=WEEKLY") && hasInterval2) return "Biweekly";
        if (upper.StartsWith("FREQ=DAILY")) return "Daily";
        if (upper.StartsWith("FREQ=WEEKLY")) return "Weekly";
        if (upper.StartsWith("FREQ=MONTHLY")) return "Monthly";
        if (upper.StartsWith("FREQ=YEARLY")) return "Yearly";

        return "None";
    }

    private static bool HasByDay(string? rrule, string day)
    {
        if (string.IsNullOrWhiteSpace(rrule))
            return false;

        var upper = rrule.ToUpperInvariant();
        // BYDAY values can be bare (MO) or position-prefixed (2TU, -1FR)
        return upper.Contains(day) && (
            upper.Contains($"BYDAY={day}") ||
            upper.Contains($",{day}") ||
            upper.EndsWith(day) ||
            // Covers position-prefixed: BYDAY=2TU or ,2TU
            System.Text.RegularExpressions.Regex.IsMatch(upper, $@"[=,]-?\d*{day}(?:,|;|$)")
        );
    }

    private static int? ParseMonthlyByDayPosition(string? rrule)
    {
        if (string.IsNullOrWhiteSpace(rrule))
            return null;

        var upper = rrule.ToUpperInvariant();
        var byDayIdx = upper.IndexOf("BYDAY=", StringComparison.Ordinal);
        if (byDayIdx < 0)
            return null;

        var byDayPart = upper[(byDayIdx + 6)..];
        var semiIdx = byDayPart.IndexOf(';', StringComparison.Ordinal);
        if (semiIdx >= 0)
            byDayPart = byDayPart[..semiIdx];

        var firstDay = byDayPart.Split(',')[0].Trim();
        if (firstDay.Length >= 3 && int.TryParse(firstDay[..^2], out var pos))
            return pos;

        return null;
    }

    /// <summary>
    /// Minimal mirror of EventEditorModel for testing From/To logic.
    /// </summary>
    private sealed class TestEventEditorModel
    {
        public Guid? Id { get; set; }
        public Guid CalendarId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Location { get; set; }
        public DateTime StartLocal { get; set; }
        public DateTime EndLocal { get; set; }
        public bool IsAllDay { get; set; }
        public string? Color { get; set; }
        public string? Url { get; set; }
        public string? RecurrenceRule { get; set; }
        public DateTime? RecurrenceEndDate { get; set; }
        public string RecurrenceType { get; set; } = "None";
        public bool RecurOnMon { get; set; }
        public bool RecurOnTue { get; set; }
        public bool RecurOnWed { get; set; }
        public bool RecurOnThu { get; set; }
        public bool RecurOnFri { get; set; }
        public bool RecurOnSat { get; set; }
        public bool RecurOnSun { get; set; }
        public int? MonthlyByDayPosition { get; set; }

        public static TestEventEditorModel From(CalendarEventDto dto) => new()
        {
            Id = dto.Id,
            CalendarId = dto.CalendarId,
            Title = dto.Title,
            Description = dto.Description,
            Location = dto.Location,
            StartLocal = dto.StartUtc.ToLocalTime(),
            EndLocal = dto.EndUtc.ToLocalTime(),
            IsAllDay = dto.IsAllDay,
            Color = dto.Color,
            Url = dto.Url,
            RecurrenceRule = dto.RecurrenceRule,
            RecurrenceType = ParseRecurrenceType(dto.RecurrenceRule),
            RecurOnMon = HasByDay(dto.RecurrenceRule, "MO"),
            RecurOnTue = HasByDay(dto.RecurrenceRule, "TU"),
            RecurOnWed = HasByDay(dto.RecurrenceRule, "WE"),
            RecurOnThu = HasByDay(dto.RecurrenceRule, "TH"),
            RecurOnFri = HasByDay(dto.RecurrenceRule, "FR"),
            RecurOnSat = HasByDay(dto.RecurrenceRule, "SA"),
            RecurOnSun = HasByDay(dto.RecurrenceRule, "SU"),
            MonthlyByDayPosition = ParseMonthlyByDayPosition(dto.RecurrenceRule)
        };

        public CreateCalendarEventDto ToCreateDto() => new()
        {
            CalendarId = CalendarId,
            Title = Title,
            Description = Description,
            Location = Location,
            StartUtc = StartLocal.ToUniversalTime(),
            EndUtc = EndLocal.ToUniversalTime(),
            IsAllDay = IsAllDay,
            Color = Color,
            Url = Url,
            RecurrenceRule = BuildRruleFromModel()
        };

        private string? BuildRruleFromModel()
        {
            return BuildRrule(RecurrenceType,
                RecurOnMon, RecurOnTue, RecurOnWed, RecurOnThu,
                RecurOnFri, RecurOnSat, RecurOnSun,
                MonthlyByDayPosition, RecurrenceEndDate);
        }
    }
}
