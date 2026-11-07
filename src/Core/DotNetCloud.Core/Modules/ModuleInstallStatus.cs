namespace DotNetCloud.Core.Modules;

/// <summary>
/// The values stored in <c>InstalledModule.Status</c>.
/// </summary>
/// <remarks>
/// The supervisor reconciles these values against the live processes: <see cref="Disabled"/> stops a
/// running module on the next reconcile pass, <see cref="Enabled"/> starts a stopped one, and
/// <see cref="RestartPending"/> restarts it. That is how the CLI's <c>module start|stop|restart</c>
/// commands take effect on a running server without an IPC channel.
/// </remarks>
public static class ModuleInstallStatus
{
    /// <summary>The module should be running.</summary>
    public const string Enabled = "Enabled";

    /// <summary>The module is installed but must not run.</summary>
    public const string Disabled = "Disabled";

    /// <summary>A newer version was detected and is awaiting admin approval.</summary>
    public const string UpdateAvailable = "UpdateAvailable";

    /// <summary>An install is in progress (transient).</summary>
    public const string Installing = "Installing";

    /// <summary>An uninstall is in progress (transient).</summary>
    public const string Uninstalling = "Uninstalling";

    /// <summary>An update is in progress (transient).</summary>
    public const string Updating = "Updating";

    /// <summary>The module repeatedly failed and needs admin intervention.</summary>
    public const string Failed = "Failed";

    /// <summary>
    /// An operator asked for a restart (for example <c>dotnetcloud module restart</c>). The
    /// supervisor restarts the process and resets the row to <see cref="Enabled"/>.
    /// </summary>
    public const string RestartPending = "RestartPending";
}
