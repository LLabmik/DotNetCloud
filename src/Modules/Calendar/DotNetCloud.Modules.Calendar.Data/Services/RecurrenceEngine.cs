using DotNetCloud.Modules.Calendar.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// RFC 5545 recurrence rule parser and occurrence expander.
/// Supports FREQ (DAILY/WEEKLY/MONTHLY/YEARLY), INTERVAL, COUNT, UNTIL,
/// BYDAY, BYMONTHDAY, BYMONTH, and BYSETPOS.
/// </summary>
public sealed class RecurrenceEngine : IRecurrenceEngine
{
    private readonly ILogger<RecurrenceEngine> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurrenceEngine"/> class.
    /// </summary>
    public RecurrenceEngine(ILogger<RecurrenceEngine> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<OccurrenceInstance> Expand(
        string rrule,
        DateTime eventStart,
        TimeSpan eventDuration,
        DateTime windowStart,
        DateTime windowEnd,
        IReadOnlySet<DateTime>? excludedDates = null,
        int maxOccurrences = 1000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rrule);

        if (windowStart > windowEnd)
        {
            return [];
        }

        var parsed = ParseRRule(rrule);
        if (parsed.Frequency == RecurrenceFrequency.None)
        {
            _logger.LogWarning("Unsupported or missing FREQ in RRULE: {RRule}", rrule);
            return [];
        }

        var results = new List<OccurrenceInstance>();
        var current = eventStart;
        var count = 0;
        var interval = parsed.Interval;

        while (true)
        {
            // Safety: stop if we've gone way past the window
            if (current > windowEnd.AddYears(1))
            {
                break;
            }

            // UNTIL and COUNT limits
            if (parsed.Until.HasValue && current > parsed.Until.Value)
            {
                break;
            }

            if (parsed.Count.HasValue && count >= parsed.Count.Value)
            {
                break;
            }

            if (results.Count >= maxOccurrences)
            {
                break;
            }

            // Generate candidate dates for this period
            var candidates = GetCandidates(parsed, current, eventStart);

            foreach (var candidate in candidates)
            {
                if (parsed.Count.HasValue && count >= parsed.Count.Value)
                {
                    break;
                }

                if (parsed.Until.HasValue && candidate > parsed.Until.Value)
                {
                    break;
                }

                if (results.Count >= maxOccurrences)
                {
                    break;
                }

                count++;

                // Skip if excluded (exception instance replaces this occurrence)
                if (excludedDates is not null && excludedDates.Contains(candidate))
                {
                    continue;
                }

                var occEnd = candidate + eventDuration;

                // Check if the occurrence overlaps the query window
                if (occEnd >= windowStart && candidate <= windowEnd)
                {
                    results.Add(new OccurrenceInstance
                    {
                        StartUtc = candidate,
                        EndUtc = occEnd
                    });
                }
            }

            // Advance to next period
            current = AdvancePeriod(parsed.Frequency, current, interval);
        }

        return results;
    }

    private static List<DateTime> GetCandidates(ParsedRRule rule, DateTime periodStart, DateTime eventStart)
    {
        return rule.Frequency switch
        {
            RecurrenceFrequency.Daily => GetDailyCandidates(periodStart),
            RecurrenceFrequency.Weekly => GetWeeklyCandidates(rule, periodStart, eventStart),
            RecurrenceFrequency.Monthly => GetMonthlyCandidates(rule, periodStart, eventStart),
            RecurrenceFrequency.Yearly => GetYearlyCandidates(rule, periodStart, eventStart),
            _ => [periodStart]
        };
    }

    private static List<DateTime> GetDailyCandidates(DateTime periodStart)
    {
        return [periodStart];
    }

    private static List<DateTime> GetWeeklyCandidates(ParsedRRule rule, DateTime periodStart, DateTime eventStart)
    {
        if (rule.ByDay.Count == 0)
        {
            return [periodStart];
        }

        var results = new List<DateTime>();
        var weekStart = GetWeekStart(periodStart, rule.WeekStart);

        for (var i = 0; i < 7; i++)
        {
            var day = weekStart.AddDays(i);
            var dayOfWeek = day.DayOfWeek;

            if (rule.ByDay.Any(bd => bd.DayOfWeek == dayOfWeek && bd.OrdinalWeek is null))
            {
                // Preserve the time from the original event
                var candidate = new DateTime(day.Year, day.Month, day.Day,
                    eventStart.Hour, eventStart.Minute, eventStart.Second, DateTimeKind.Utc);

                if (candidate >= eventStart)
                {
                    results.Add(candidate);
                }
            }
        }

        results.Sort();
        return results;
    }

    private static List<DateTime> GetMonthlyCandidates(ParsedRRule rule, DateTime periodStart, DateTime eventStart)
    {
        var results = new List<DateTime>();

        if (rule.ByMonthDay.Count > 0)
        {
            foreach (var dayOfMonth in rule.ByMonthDay)
            {
                var daysInMonth = DateTime.DaysInMonth(periodStart.Year, periodStart.Month);
                var actualDay = dayOfMonth > 0 ? dayOfMonth : daysInMonth + dayOfMonth + 1;

                if (actualDay >= 1 && actualDay <= daysInMonth)
                {
                    var candidate = new DateTime(periodStart.Year, periodStart.Month, actualDay,
                        eventStart.Hour, eventStart.Minute, eventStart.Second, DateTimeKind.Utc);

                    if (candidate >= eventStart)
                    {
                        results.Add(candidate);
                    }
                }
            }
        }
        else if (rule.ByDay.Count > 0)
        {
            // e.g., BYDAY=2MO means second Monday of the month
            foreach (var byDay in rule.ByDay)
            {
                var candidates = GetNthDayOfMonth(periodStart.Year, periodStart.Month, byDay, eventStart);
                results.AddRange(candidates.Where(c => c >= eventStart));
            }

            // Apply BYSETPOS filter if present
            if (rule.BySetPos.Count > 0)
            {
                results.Sort();
                var filtered = new List<DateTime>();
                foreach (var pos in rule.BySetPos)
                {
                    var index = pos > 0 ? pos - 1 : results.Count + pos;
                    if (index >= 0 && index < results.Count)
                    {
                        filtered.Add(results[index]);
                    }
                }
                results = filtered;
            }
        }
        else
        {
            // Default: same day of month as DTSTART
            var daysInMonth = DateTime.DaysInMonth(periodStart.Year, periodStart.Month);
            var day = Math.Min(eventStart.Day, daysInMonth);
            var candidate = new DateTime(periodStart.Year, periodStart.Month, day,
                eventStart.Hour, eventStart.Minute, eventStart.Second, DateTimeKind.Utc);
            if (candidate >= eventStart)
            {
                results.Add(candidate);
            }
        }

        results.Sort();
        return results;
    }

    private static List<DateTime> GetYearlyCandidates(ParsedRRule rule, DateTime periodStart, DateTime eventStart)
    {
        var months = rule.ByMonth.Count > 0 ? rule.ByMonth : [eventStart.Month];
        var results = new List<DateTime>();

        foreach (var month in months)
        {
            if (rule.ByMonthDay.Count > 0)
            {
                foreach (var dayOfMonth in rule.ByMonthDay)
                {
                    var daysInMonth = DateTime.DaysInMonth(periodStart.Year, month);
                    var actualDay = dayOfMonth > 0 ? dayOfMonth : daysInMonth + dayOfMonth + 1;

                    if (actualDay >= 1 && actualDay <= daysInMonth)
                    {
                        var candidate = new DateTime(periodStart.Year, month, actualDay,
                            eventStart.Hour, eventStart.Minute, eventStart.Second, DateTimeKind.Utc);
                        if (candidate >= eventStart)
                        {
                            results.Add(candidate);
                        }
                    }
                }
            }
            else if (rule.ByDay.Count > 0)
            {
                foreach (var byDay in rule.ByDay)
                {
                    var candidates = GetNthDayOfMonth(periodStart.Year, month, byDay, eventStart);
                    results.AddRange(candidates.Where(c => c >= eventStart));
                }
            }
            else
            {
                var daysInMonth = DateTime.DaysInMonth(periodStart.Year, month);
                var day = Math.Min(eventStart.Day, daysInMonth);
                var candidate = new DateTime(periodStart.Year, month, day,
                    eventStart.Hour, eventStart.Minute, eventStart.Second, DateTimeKind.Utc);
                if (candidate >= eventStart)
                {
                    results.Add(candidate);
                }
            }
        }

        results.Sort();
        return results;
    }

    private static List<DateTime> GetNthDayOfMonth(int year, int month, ByDayEntry byDay, DateTime eventStart)
    {
        var results = new List<DateTime>();
        var daysInMonth = DateTime.DaysInMonth(year, month);

        if (byDay.OrdinalWeek.HasValue)
        {
            var ordinal = byDay.OrdinalWeek.Value;

            if (ordinal > 0)
            {
                // e.g., 2MO = second Monday
                var found = 0;
                for (var d = 1; d <= daysInMonth; d++)
                {
                    var date = new DateTime(year, month, d, eventStart.Hour, eventStart.Minute, eventStart.Second, DateTimeKind.Utc);
                    if (date.DayOfWeek == byDay.DayOfWeek)
                    {
                        found++;
                        if (found == ordinal)
                        {
                            results.Add(date);
                            break;
                        }
                    }
                }
            }
            else
            {
                // Negative: -1MO = last Monday
                var found = 0;
                for (var d = daysInMonth; d >= 1; d--)
                {
                    var date = new DateTime(year, month, d, eventStart.Hour, eventStart.Minute, eventStart.Second, DateTimeKind.Utc);
                    if (date.DayOfWeek == byDay.DayOfWeek)
                    {
                        found++;
                        if (found == Math.Abs(ordinal))
                        {
                            results.Add(date);
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            // All occurrences of that day in the month (e.g., all Mondays)
            for (var d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(year, month, d, eventStart.Hour, eventStart.Minute, eventStart.Second, DateTimeKind.Utc);
                if (date.DayOfWeek == byDay.DayOfWeek)
                {
                    results.Add(date);
                }
            }
        }

        return results;
    }

    private static DateTime GetWeekStart(DateTime date, DayOfWeek weekStart)
    {
        var diff = ((int)date.DayOfWeek - (int)weekStart + 7) % 7;
        return date.AddDays(-diff).Date;
    }

    private static DateTime AdvancePeriod(RecurrenceFrequency frequency, DateTime current, int interval)
    {
        return frequency switch
        {
            RecurrenceFrequency.Daily => current.AddDays(interval),
            RecurrenceFrequency.Weekly => current.AddDays(7 * interval),
            RecurrenceFrequency.Monthly => current.AddMonths(interval),
            RecurrenceFrequency.Yearly => current.AddYears(interval),
            _ => current.AddDays(1) // Should not happen
        };
    }

    internal static ParsedRRule ParseRRule(string rrule)
    {
        var result = new ParsedRRule();

        // Strip optional "RRULE:" prefix
        var rule = rrule.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase)
            ? rrule[6..]
            : rrule;

        var parts = rule.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = part[..eqIndex].Trim().ToUpperInvariant();
            var value = part[(eqIndex + 1)..].Trim();

            switch (key)
            {
                case "FREQ":
                    result.Frequency = ParseFrequency(value);
                    break;

                case "INTERVAL":
                    if (int.TryParse(value, out var interval) && interval > 0)
                        result.Interval = interval;
                    break;

                case "COUNT":
                    if (int.TryParse(value, out var count) && count > 0)
                        result.Count = count;
                    break;

                case "UNTIL":
                    result.Until = ParseDateTime(value);
                    break;

                case "BYDAY":
                    result.ByDay = ParseByDay(value);
                    break;

                case "BYMONTHDAY":
                    result.ByMonthDay = ParseIntList(value);
                    break;

                case "BYMONTH":
                    result.ByMonth = ParseIntList(value);
                    break;

                case "BYSETPOS":
                    result.BySetPos = ParseIntList(value);
                    break;

                case "WKST":
                    result.WeekStart = ParseDayOfWeek(value) ?? DayOfWeek.Monday;
                    break;
            }
        }

        return result;
    }

    private static RecurrenceFrequency ParseFrequency(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "DAILY" => RecurrenceFrequency.Daily,
            "WEEKLY" => RecurrenceFrequency.Weekly,
            "MONTHLY" => RecurrenceFrequency.Monthly,
            "YEARLY" => RecurrenceFrequency.Yearly,
            _ => RecurrenceFrequency.None
        };
    }

    private static DateTime? ParseDateTime(string value)
    {
        // Formats: 20250331T000000Z or 20250331T000000 or 20250331
        var s = value.TrimEnd('Z', 'z');

        if (s.Length == 8 &&
            int.TryParse(s[..4], out var y) &&
            int.TryParse(s[4..6], out var m) &&
            int.TryParse(s[6..8], out var d))
        {
            return new DateTime(y, m, d, 23, 59, 59, DateTimeKind.Utc);
        }

        if (s.Length >= 15 && s[8] == 'T' &&
            int.TryParse(s[..4], out var y2) &&
            int.TryParse(s[4..6], out var m2) &&
            int.TryParse(s[6..8], out var d2) &&
            int.TryParse(s[9..11], out var h) &&
            int.TryParse(s[11..13], out var min) &&
            int.TryParse(s[13..15], out var sec))
        {
            return new DateTime(y2, m2, d2, h, min, sec, DateTimeKind.Utc);
        }

        return null;
    }

    private static List<ByDayEntry> ParseByDay(string value)
    {
        var entries = new List<ByDayEntry>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = token.Trim();
            int? ordinal = null;

            // Extract optional ordinal prefix (e.g., "2MO", "-1FR")
            var dayStart = 0;
            for (var i = 0; i < trimmed.Length; i++)
            {
                if (char.IsLetter(trimmed[i]))
                {
                    dayStart = i;
                    break;
                }
            }

            if (dayStart > 0 && int.TryParse(trimmed[..dayStart], out var ord))
            {
                ordinal = ord;
            }

            var dayAbbrev = trimmed[dayStart..];
            var dow = ParseDayOfWeek(dayAbbrev);
            if (dow.HasValue)
            {
                entries.Add(new ByDayEntry(dow.Value, ordinal));
            }
        }

        return entries;
    }

    private static DayOfWeek? ParseDayOfWeek(string abbrev)
    {
        return abbrev.ToUpperInvariant() switch
        {
            "MO" => DayOfWeek.Monday,
            "TU" => DayOfWeek.Tuesday,
            "WE" => DayOfWeek.Wednesday,
            "TH" => DayOfWeek.Thursday,
            "FR" => DayOfWeek.Friday,
            "SA" => DayOfWeek.Saturday,
            "SU" => DayOfWeek.Sunday,
            _ => null
        };
    }

    private static List<int> ParseIntList(string value)
    {
        var result = new List<int>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token.Trim(), out var n))
            {
                result.Add(n);
            }
        }
        return result;
    }

    internal sealed class ParsedRRule
    {
        public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.None;
        public int Interval { get; set; } = 1;
        public int? Count { get; set; }
        public DateTime? Until { get; set; }
        public List<ByDayEntry> ByDay { get; set; } = [];
        public List<int> ByMonthDay { get; set; } = [];
        public List<int> ByMonth { get; set; } = [];
        public List<int> BySetPos { get; set; } = [];
        public DayOfWeek WeekStart { get; set; } = DayOfWeek.Monday;
    }

    internal enum RecurrenceFrequency { None, Daily, Weekly, Monthly, Yearly }

    internal sealed record ByDayEntry(DayOfWeek DayOfWeek, int? OrdinalWeek);
}
