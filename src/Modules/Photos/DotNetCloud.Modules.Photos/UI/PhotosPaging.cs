namespace DotNetCloud.Modules.Photos.UI;

/// <summary>
/// Pure paging math for the Photos grid. Kept free of component state so the
/// viewport-driven page sizing, page-count and resize calculations are unit
/// testable.
/// </summary>
internal static class PhotosPaging
{
    /// <summary>
    /// Page size used before the JS layout observer has measured the viewport
    /// (and the permanent fallback when JavaScript is unavailable).
    /// </summary>
    internal const int DefaultPageSize = 60;

    /// <summary>Upper bound for a single page, so a huge viewport can't request everything.</summary>
    internal const int MaxPageSize = 500;

    /// <summary>Clamps a page size reported by the browser into a usable range.</summary>
    internal static int NormalizePageSize(int pageSize)
        => Math.Clamp(pageSize, 1, MaxPageSize);

    /// <summary>
    /// Number of pages needed to show <paramref name="totalCount"/> items. Always
    /// at least 1 so an empty section still reads "Page 1 of 1".
    /// </summary>
    internal static int ComputeTotalPages(int totalCount, int pageSize)
    {
        var size = NormalizePageSize(pageSize);
        if (totalCount <= 0)
            return 1;

        return Math.Max(1, (totalCount + size - 1) / size);
    }

    /// <summary>Clamps a zero-based page index to the range available for the current total.</summary>
    internal static int ClampPage(int page, int totalCount, int pageSize)
    {
        var lastPage = ComputeTotalPages(totalCount, pageSize) - 1;
        return Math.Clamp(page, 0, Math.Max(0, lastPage));
    }

    /// <summary>
    /// Computes the page index to use after a page-size change so the item at
    /// <paramref name="firstOffset"/> (the current first-visible item) stays on
    /// screen. Clamped to a valid page index for <paramref name="totalCount"/>.
    /// </summary>
    internal static int ComputePageForResize(int firstOffset, int newSize, int totalCount)
    {
        if (newSize <= 0)
            return 0;

        var page = Math.Max(0, firstOffset) / newSize;
        if (totalCount <= 0)
            return page;

        var lastPage = Math.Max(0, (totalCount + newSize - 1) / newSize - 1);
        return Math.Clamp(page, 0, lastPage);
    }

    /// <summary>
    /// Slices one page out of an in-memory list (the sections that load their whole
    /// list up front). Returns the source unchanged when it already fits one page,
    /// so the common case allocates nothing.
    /// </summary>
    internal static IReadOnlyList<T> SlicePage<T>(IReadOnlyList<T> source, int page, int pageSize)
    {
        var size = NormalizePageSize(pageSize);
        var start = Math.Max(0, page) * size;

        if (start == 0 && source.Count <= size)
            return source;

        if (start >= source.Count)
            return [];

        return source.Skip(start).Take(size).ToList();
    }
}
