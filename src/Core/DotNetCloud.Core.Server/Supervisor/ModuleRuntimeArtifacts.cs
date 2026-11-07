using DotNetCloud.Core.Modules.Supervisor;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Filesystem side of socket transport: creates the runtime directory, removes stale socket files
/// and restricts the socket to the service account.
/// </summary>
/// <remarks>
/// <para>
/// Kestrel deletes a Unix socket file on a clean shutdown, but a killed process leaves it behind and
/// the next bind then fails with "address already in use". The supervisor therefore removes the
/// file before every spawn and after every stop. Windows named pipes are ephemeral and need none of
/// this.
/// </para>
/// <para>
/// File permissions are defence in depth only: <c>0600</c> keys on the OS user, so any sibling
/// module running as the same account can still connect. The API identity control is the
/// supervisor-issued module credential, not the filesystem.
/// </para>
/// </remarks>
internal static class ModuleRuntimeArtifacts
{
    /// <summary>
    /// Creates the module runtime directory with owner-only permissions.
    /// </summary>
    /// <param name="runDirectory">The absolute runtime directory.</param>
    /// <returns>The directory path.</returns>
    public static string EnsureRunDirectory(string runDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runDirectory);

        Directory.CreateDirectory(runDirectory);
        TrySetMode(
            runDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return runDirectory;
    }

    /// <summary>
    /// Prepares the endpoint's transport artifacts before a module host starts.
    /// </summary>
    /// <param name="endpoint">The module endpoint.</param>
    /// <param name="runDirectory">The module runtime directory, created when supplied.</param>
    /// <returns><see langword="true"/> when the endpoint is ready to bind.</returns>
    public static bool Prepare(ModuleEndpoint endpoint, string? runDirectory)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!string.IsNullOrWhiteSpace(runDirectory))
        {
            EnsureRunDirectory(runDirectory);
        }

        if (endpoint.Transport != ModuleTransportKind.UnixDomainSocket)
        {
            // Named pipes are released by the OS when the host exits; TCP has no artifact.
            return true;
        }

        if (!TryResolveSocketPath(endpoint, out var socketPath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(socketPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            TrySetMode(
                directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        try
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Restricts an existing Unix socket file to the service account (read/write only).
    /// </summary>
    /// <param name="endpoint">The module endpoint.</param>
    /// <returns><see langword="true"/> when the mode was applied (or the endpoint is not a socket).</returns>
    public static bool RestrictSocket(ModuleEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (endpoint.Transport != ModuleTransportKind.UnixDomainSocket
            || !TryResolveSocketPath(endpoint, out var socketPath)
            || !File.Exists(socketPath))
        {
            return endpoint.Transport != ModuleTransportKind.UnixDomainSocket;
        }

        return TrySetMode(socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>
    /// Removes the endpoint's transport artifacts after a module host stops.
    /// </summary>
    /// <param name="endpoint">The module endpoint.</param>
    public static void Remove(ModuleEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (endpoint.Transport != ModuleTransportKind.UnixDomainSocket
            || !TryResolveSocketPath(endpoint, out var socketPath))
        {
            return;
        }

        try
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a stale socket is removed before the next spawn anyway.
        }
    }

    /// <summary>
    /// Gets the socket file mode a module endpoint should have, or <see langword="null"/> when the
    /// endpoint is not a Unix socket.
    /// </summary>
    /// <param name="endpoint">The module endpoint.</param>
    /// <returns>The expected mode.</returns>
    public static UnixFileMode? ExpectedSocketMode(ModuleEndpoint endpoint) =>
        endpoint.Transport == ModuleTransportKind.UnixDomainSocket
            ? UnixFileMode.UserRead | UnixFileMode.UserWrite
            : null;

    /// <summary>
    /// Resolves the socket path and proves it stays inside its own directory.
    /// </summary>
    /// <remarks>
    /// The socket path is derived from a module id, so it must go through the same
    /// <see cref="Path.GetFullPath(string)"/> plus non-weak containment guard as every other
    /// module-derived path before any <see cref="File"/> call touches it.
    /// </remarks>
    private static bool TryResolveSocketPath(ModuleEndpoint endpoint, out string socketPath)
    {
        socketPath = string.Empty;

        if (!Path.IsPathRooted(endpoint.Address))
        {
            return false;
        }

        string directory;
        try
        {
            directory = Path.GetDirectoryName(Path.GetFullPath(endpoint.Address)) ?? string.Empty;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (directory.Length == 0)
        {
            return false;
        }

        return PathContainment.TryResolveWithin(directory, endpoint.Address, out socketPath);
    }

    private static bool TrySetMode(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            File.SetUnixFileMode(path, mode);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Best effort: read-only or exotic filesystems must not stop module startup.
            return false;
        }
    }
}
