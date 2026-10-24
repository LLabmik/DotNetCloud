namespace DotNetCloud.Core.Server.Diagnostics;

/// <summary>
/// Pure CPU percentage math shared by <see cref="SystemMetricsSampler"/>.
/// </summary>
internal static class CpuUsageCalculator
{
    /// <summary>
    /// Computes CPU usage as a percentage of total machine capacity from a
    /// processor-time delta over a wall-clock window.
    /// </summary>
    /// <param name="cpuDelta">Processor time consumed during the window.</param>
    /// <param name="wallDelta">Wall-clock duration of the window.</param>
    /// <param name="processorCount">Logical processor count.</param>
    /// <returns>
    /// A value in the range 0–100 rounded to two decimals, or <c>null</c> when the
    /// window or processor count is not usable.
    /// </returns>
    internal static double? Compute(TimeSpan cpuDelta, TimeSpan wallDelta, int processorCount)
    {
        if (wallDelta <= TimeSpan.Zero || processorCount <= 0)
        {
            return null;
        }

        var capacityMs = wallDelta.TotalMilliseconds * processorCount;
        if (capacityMs <= 0)
        {
            return null;
        }

        var percent = cpuDelta.TotalMilliseconds / capacityMs * 100.0;
        if (percent < 0)
        {
            percent = 0;
        }
        else if (percent > 100.0)
        {
            percent = 100.0;
        }

        return Math.Round(percent, 2);
    }
}
