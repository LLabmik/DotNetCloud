namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Carries the core-signed user assertion for the work currently executing on this logical flow.
/// </summary>
/// <remarks>
/// <para>
/// A module hosts many users' requests concurrently, so the assertion cannot be a static or
/// container-scoped value — it is per-request state. <see cref="AsyncLocal{T}"/> gives it the
/// right lifetime, and <see cref="ModuleIdentityMetadata.Create"/> reads it automatically, which
/// means every existing module→core capability call starts carrying the user's identity without
/// threading a parameter through each service.
/// </para>
/// <para>
/// Background work has no ambient assertion, so it calls the core as the module itself. That is
/// the intended outcome: nothing a module starts on its own timer may name a user.
/// </para>
/// </remarks>
public static class ModuleUserAssertionContext
{
    private static readonly AsyncLocal<string?> CurrentAssertion = new();

    /// <summary>
    /// Gets the assertion for the current logical flow, or <see langword="null"/> when the flow is
    /// not acting for an end user.
    /// </summary>
    public static string? Current => CurrentAssertion.Value;

    /// <summary>
    /// Sets the assertion for the current logical flow, returning a handle that restores the
    /// previous value when disposed.
    /// </summary>
    /// <param name="assertion">The assertion token, or <see langword="null"/> to clear it.</param>
    /// <returns>A scope that restores the prior value.</returns>
    public static IDisposable Set(string? assertion)
    {
        var previous = CurrentAssertion.Value;
        CurrentAssertion.Value = assertion;
        return new Restore(previous);
    }

    private sealed class Restore : IDisposable
    {
        private readonly string? _previous;
        private bool _disposed;

        public Restore(string? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentAssertion.Value = _previous;
            _disposed = true;
        }
    }
}
