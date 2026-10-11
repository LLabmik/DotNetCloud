namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Test <see cref="IProgress{T}"/> that invokes its handler synchronously on the reporting thread.
/// </summary>
/// <remarks>
/// <see cref="Progress{T}"/> hands every report to the captured <see cref="SynchronizationContext"/>
/// — falling back to the thread pool when there is none — so reports may not have been applied yet
/// when the operation under test returns. Tests that assert on reported progress right after awaiting
/// a service therefore have to sleep an arbitrary amount and still flake under load; the
/// <c>EnrichAll_ProgressReporting_AllPhases</c> failure on CI was exactly that.
/// Use this type instead of <see cref="Progress{T}"/> whenever a test inspects what was reported.
/// </remarks>
/// <typeparam name="T">The progress value type.</typeparam>
internal sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="SynchronousProgress{T}"/> class.
    /// </summary>
    /// <param name="handler">Handler invoked synchronously for every reported value.</param>
    public SynchronousProgress(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    /// <inheritdoc/>
    public void Report(T value) => _handler(value);
}
