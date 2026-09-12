using Microsoft.Extensions.Logging;
using AndroidLog = Android.Util.Log;
using AndroidLogPriority = Android.Util.LogPriority;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Routes <see cref="ILogger"/> output to Android's logcat so on-device diagnostics are
/// visible through <c>adb logcat</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>builder.Logging.AddDebug()</c> writes through <see cref="System.Diagnostics.Debug"/>,
/// which is only observable while a debugger is attached. That leaves background work — most
/// notably <c>MediaAutoUploadService</c>, whose "skipped because…" branches only log — entirely
/// invisible in the field, making a stalled upload indistinguishable from a failed one.
/// </para>
/// <para>
/// Registering this provider makes every <see cref="ILogger"/> call appear under the
/// <c>DotNetCloud</c> logcat tag in both Debug and Release builds.
/// </para>
/// </remarks>
internal sealed class AndroidLogLoggerProvider : ILoggerProvider
{
    /// <summary>Logcat tag applied to every entry emitted by this provider.</summary>
    private const string Tag = "DotNetCloud";

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new AndroidLogLogger(categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
        // Android.Util.Log is a static facade over liblog; there is nothing to release.
    }

    /// <summary>Writes formatted log entries to logcat under the <see cref="Tag"/> tag.</summary>
    private sealed class AndroidLogLogger : ILogger
    {
        private readonly string _category;

        /// <summary>Initializes a logger for the supplied category name.</summary>
        /// <param name="categoryName">Full logger category (typically the type's full name).</param>
        internal AndroidLogLogger(string categoryName)
        {
            // Keep the logcat prefix short: use only the final segment of the category name
            // (e.g. "MediaAutoUploadService" rather than the whole namespace).
            var lastDot = categoryName.LastIndexOf('.');
            _category = lastDot >= 0 && lastDot < categoryName.Length - 1
                ? categoryName[(lastDot + 1)..]
                : categoryName;
        }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            AndroidLog.WriteLine(ToPriority(logLevel), Tag, $"[{_category}] {formatter(state, exception)}");

            if (exception is not null)
                AndroidLog.WriteLine(AndroidLogPriority.Error, Tag, $"[{_category}] {exception}");
        }

        /// <summary>Maps a <see cref="LogLevel"/> to the nearest Android <see cref="AndroidLogPriority"/>.</summary>
        private static AndroidLogPriority ToPriority(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace or LogLevel.Debug => AndroidLogPriority.Debug,
            LogLevel.Information => AndroidLogPriority.Info,
            LogLevel.Warning => AndroidLogPriority.Warn,
            LogLevel.Error => AndroidLogPriority.Error,
            LogLevel.Critical => AndroidLogPriority.Error,
            _ => AndroidLogPriority.Verbose
        };
    }
}
