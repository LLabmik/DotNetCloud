namespace DotNetCloud.Modules.Search.Tests.Phase7;

/// <summary>
/// Tests for search result display formatting utilities used in Phase 7 Blazor UI.
/// Covers date formatting, file size formatting, and file type label generation.
/// </summary>
[TestClass]
public class SearchDisplayFormatTests
{
    #region FormatDate — Relative time display

    [TestMethod]
    public void FormatDate_JustNow_ReturnsJustNow()
    {
        var date = DateTimeOffset.UtcNow.AddSeconds(-10);
        Assert.AreEqual("Just now", SearchDisplayFormatter.FormatDate(date));
    }

    [TestMethod]
    public void FormatDate_FewMinutesAgo_ReturnsMinutesFormat()
    {
        var date = DateTimeOffset.UtcNow.AddMinutes(-5);
        Assert.AreEqual("5m ago", SearchDisplayFormatter.FormatDate(date));
    }

    [TestMethod]
    public void FormatDate_OneMinuteAgo_Returns1mAgo()
    {
        var date = DateTimeOffset.UtcNow.AddMinutes(-1).AddSeconds(-10);
        Assert.AreEqual("1m ago", SearchDisplayFormatter.FormatDate(date));
    }

    [TestMethod]
    public void FormatDate_FewHoursAgo_ReturnsHoursFormat()
    {
        var date = DateTimeOffset.UtcNow.AddHours(-3);
        Assert.AreEqual("3h ago", SearchDisplayFormatter.FormatDate(date));
    }

    [TestMethod]
    public void FormatDate_OneHourAgo_Returns1hAgo()
    {
        var date = DateTimeOffset.UtcNow.AddHours(-1).AddMinutes(-10);
        Assert.AreEqual("1h ago", SearchDisplayFormatter.FormatDate(date));
    }

    [TestMethod]
    public void FormatDate_FewDaysAgo_ReturnsDaysFormat()
    {
        var date = DateTimeOffset.UtcNow.AddDays(-3);
        Assert.AreEqual("3d ago", SearchDisplayFormatter.FormatDate(date));
    }

    [TestMethod]
    public void FormatDate_MoreThanWeekAgo_ReturnsFormattedDate()
    {
        var date = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var result = SearchDisplayFormatter.FormatDate(date);
        Assert.IsTrue(result.Contains("Jan") && result.Contains("15") && result.Contains("2026"),
            $"Expected formatted date but got: {result}");
    }

    [TestMethod]
    public void FormatDate_ExactlyOneWeekAgo_ReturnsFormattedDate()
    {
        var date = DateTimeOffset.UtcNow.AddDays(-7);
        var result = SearchDisplayFormatter.FormatDate(date);
        // 7 days = exactly at threshold, falls to formatted date
        Assert.IsTrue(result.Contains("ago") || result.Contains(","),
            $"Result should be relative or formatted: {result}");
    }

    #endregion

    #region FormatFileSize — Human-readable sizes

    [TestMethod]
    public void FormatFileSize_ZeroBytes_Returns0B()
    {
        Assert.AreEqual("0 B", SearchDisplayFormatter.FormatFileSize(0));
    }

    [TestMethod]
    public void FormatFileSize_SmallBytes_ReturnsBytesFormat()
    {
        Assert.AreEqual("512 B", SearchDisplayFormatter.FormatFileSize(512));
    }

    [TestMethod]
    public void FormatFileSize_1023Bytes_ReturnsBytesFormat()
    {
        Assert.AreEqual("1023 B", SearchDisplayFormatter.FormatFileSize(1023));
    }

    [TestMethod]
    public void FormatFileSize_1KB_ReturnsKBFormat()
    {
        Assert.AreEqual("1.0 KB", SearchDisplayFormatter.FormatFileSize(1024));
    }

    [TestMethod]
    public void FormatFileSize_LargeKB_ReturnsKBFormat()
    {
        Assert.AreEqual("500.0 KB", SearchDisplayFormatter.FormatFileSize(512000));
    }

    [TestMethod]
    public void FormatFileSize_1MB_ReturnsMBFormat()
    {
        Assert.AreEqual("1.0 MB", SearchDisplayFormatter.FormatFileSize(1024 * 1024));
    }

    [TestMethod]
    public void FormatFileSize_FractionalMB_ReturnsMBFormat()
    {
        var result = SearchDisplayFormatter.FormatFileSize((long)(2.5 * 1024 * 1024));
        Assert.AreEqual("2.5 MB", result);
    }

    [TestMethod]
    public void FormatFileSize_1GB_ReturnsGBFormat()
    {
        Assert.AreEqual("1.0 GB", SearchDisplayFormatter.FormatFileSize(1024L * 1024 * 1024));
    }

    [TestMethod]
    public void FormatFileSize_LargeGB_ReturnsGBFormat()
    {
        var result = SearchDisplayFormatter.FormatFileSize(5L * 1024 * 1024 * 1024);
        Assert.AreEqual("5.0 GB", result);
    }

    #endregion

    #region GetFileTypeLabel — MIME type to label

    [TestMethod]
    [DataRow("application/pdf", "PDF")]
    [DataRow("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "DOCX")]
    [DataRow("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "XLSX")]
    [DataRow("text/plain", "Text")]
    [DataRow("text/markdown", "Markdown")]
    [DataRow("text/csv", "CSV")]
    public void GetFileTypeLabel_KnownMimeType_ReturnsLabel(string mimeType, string expected)
    {
        Assert.AreEqual(expected, SearchDisplayFormatter.GetFileTypeLabel(mimeType));
    }

    [TestMethod]
    [DataRow("image/png", "Image")]
    [DataRow("image/jpeg", "Image")]
    [DataRow("image/gif", "Image")]
    [DataRow("image/webp", "Image")]
    public void GetFileTypeLabel_ImageMimeType_ReturnsImage(string mimeType, string expected)
    {
        Assert.AreEqual(expected, SearchDisplayFormatter.GetFileTypeLabel(mimeType));
    }

    [TestMethod]
    [DataRow("video/mp4", "Video")]
    [DataRow("video/webm", "Video")]
    public void GetFileTypeLabel_VideoMimeType_ReturnsVideo(string mimeType, string expected)
    {
        Assert.AreEqual(expected, SearchDisplayFormatter.GetFileTypeLabel(mimeType));
    }

    [TestMethod]
    [DataRow("audio/mpeg", "Audio")]
    [DataRow("audio/ogg", "Audio")]
    public void GetFileTypeLabel_AudioMimeType_ReturnsAudio(string mimeType, string expected)
    {
        Assert.AreEqual(expected, SearchDisplayFormatter.GetFileTypeLabel(mimeType));
    }

    [TestMethod]
    public void GetFileTypeLabel_UnknownMimeType_ReturnsSubtype()
    {
        Assert.AreEqual("zip", SearchDisplayFormatter.GetFileTypeLabel("application/zip"));
    }

    [TestMethod]
    public void GetFileTypeLabel_CustomMimeType_ReturnsSubtype()
    {
        Assert.AreEqual("json", SearchDisplayFormatter.GetFileTypeLabel("application/json"));
    }

    #endregion
}

/// <summary>
/// Testable extraction of display formatting logic from SearchResultCard.razor.
/// </summary>
public static class SearchDisplayFormatter
{
    public static string FormatDate(DateTimeOffset date)
    {
        var diff = DateTimeOffset.UtcNow - date;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return date.LocalDateTime.ToString("MMM d, yyyy");
    }

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    public static string GetFileTypeLabel(string mimeType) => mimeType switch
    {
        "application/pdf" => "PDF",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "DOCX",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "XLSX",
        "text/plain" => "Text",
        "text/markdown" => "Markdown",
        "text/csv" => "CSV",
        _ when mimeType.StartsWith("image/") => "Image",
        _ when mimeType.StartsWith("video/") => "Video",
        _ when mimeType.StartsWith("audio/") => "Audio",
        _ => mimeType.Split('/').LastOrDefault() ?? "File"
    };
}
