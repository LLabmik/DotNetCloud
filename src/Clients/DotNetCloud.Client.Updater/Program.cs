using System.Diagnostics;

// DotNetCloud desktop client updater helper (Windows).
//
// Launched by ClientUpdateService.ApplyUpdateWindowsAsync with:
//   --pid <pid>       process to wait for before replacing files
//   --source <dir>    extracted payload directory (new files)
//   --target <dir>    install directory to overwrite
//   --exe <path>      application executable to relaunch
//
// The process runs elevated (see app.manifest) so it can write to
// %ProgramFiles%, waits for the main app to exit, copies the new files,
// and relaunches the application.

var logPath = Path.Combine(
    Path.GetTempPath(),
    "DotNetCloud",
    "updates",
    $"dotnetcloud-updater-{Environment.ProcessId}.log");
Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

void Log(string message)
{
    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
    Console.WriteLine(line);
    try
    {
        File.AppendAllText(logPath, line + Environment.NewLine);
    }
    catch
    {
        // Logging is best-effort; never mask the actual failure.
    }
}

try
{
    var cliArgs = ParseArgs(Environment.GetCommandLineArgs().Skip(1));
    var pid = cliArgs.GetValueOrDefault("pid");
    var source = cliArgs.GetValueOrDefault("source");
    var target = cliArgs.GetValueOrDefault("target");
    var exe = cliArgs.GetValueOrDefault("exe");

    if (string.IsNullOrWhiteSpace(source) ||
        string.IsNullOrWhiteSpace(target) ||
        string.IsNullOrWhiteSpace(exe))
    {
        Log("Missing required arguments. Expected --pid <pid> --source <dir> --target <dir> --exe <path>.");
        return 1;
    }

    if (!string.IsNullOrWhiteSpace(pid) && int.TryParse(pid, out var processId))
    {
        Log($"Waiting for process {processId} to exit…");
        try
        {
            using var process = Process.GetProcessById(processId);
            process.WaitForExit();
            Log($"Process {processId} exited.");
        }
        catch (ArgumentException)
        {
            Log($"Process {processId} is not running; continuing.");
        }
    }

    Log($"Copying files from {source} to {target}…");
    CopyDirectory(source, target);
    Log("Copy complete.");

    Log($"Launching {exe}.");
    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
    Log("Launch initiated.");
    return 0;
}
catch (Exception ex)
{
    Log($"Updater failed: {ex}");
    return 2;
}

static Dictionary<string, string> ParseArgs(IEnumerable<string> args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var list = args.ToList();
    for (var i = 0; i < list.Count; i++)
    {
        var token = list[i];
        if (token.StartsWith("--", StringComparison.Ordinal))
        {
            var key = token[2..];
            var value = i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? list[i + 1]
                : string.Empty;
            result[key] = value;
            if (value.Length > 0)
                i++;
        }
    }

    return result;
}

static void CopyDirectory(string source, string target)
{
    Directory.CreateDirectory(target);

    foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(source, dir);
        Directory.CreateDirectory(Path.Combine(target, relative));
    }

    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(source, file);
        var destination = Path.Combine(target, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        CopyFileWithRetry(file, destination);
    }
}

static void CopyFileWithRetry(string sourceFile, string destinationFile)
{
    const int maxAttempts = 10;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            File.Copy(sourceFile, destinationFile, overwrite: true);
            return;
        }
        catch (IOException) when (attempt < maxAttempts)
        {
            Thread.Sleep(300 * attempt);
        }
        catch (UnauthorizedAccessException) when (attempt < maxAttempts)
        {
            Thread.Sleep(300 * attempt);
        }
    }
}
