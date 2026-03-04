using System.Diagnostics;
using System.Net.Sockets;

namespace DotNetCloud.Integration.Tests.Infrastructure;

/// <summary>
/// Manages a Docker container for database integration testing.
/// Starts a container before tests, tears it down after.
/// </summary>
/// <remarks>
/// Supports two Docker execution modes:
/// <list type="bullet">
///   <item><b>Native:</b> Docker CLI installed directly on the host (Linux, macOS, Docker Desktop).</item>
///   <item><b>WSL:</b> Docker Engine installed inside WSL 2 on Windows — commands are routed
///   through <c>wsl docker ...</c>. WSL 2 automatically forwards container ports to
///   <c>localhost</c> on the Windows host.</item>
/// </list>
/// Detection is automatic: native Docker is tried first, then WSL fallback.
/// </remarks>
internal sealed class DatabaseContainerFixture : IAsyncDisposable
{
    private readonly DatabaseContainerConfig _config;
    private string? _containerId;

    // Static Docker detection state (shared across all fixture instances, detected once per test session)
    private static bool s_detectionDone;
    private static bool s_dockerFound;
    private static bool s_useWsl;

    /// <summary>
    /// Gets the connection string for the running container.
    /// </summary>
    public string? ConnectionString { get; private set; }

    /// <summary>
    /// Gets a value indicating whether Docker is available on this machine.
    /// </summary>
    public bool IsDockerAvailable { get; private set; }

    /// <summary>
    /// Gets the host port mapped to the container's database port.
    /// </summary>
    public int HostPort { get; private set; }

    public DatabaseContainerFixture(DatabaseContainerConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Starts the Docker container and waits for the database to be ready.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the container started and the database is ready.</returns>
    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        IsDockerAvailable = await DetectDockerAsync(cancellationToken);
        if (!IsDockerAvailable)
        {
            return false;
        }

        // Pick a random port in ephemeral range
        HostPort = Random.Shared.Next(49152, 65535);

        var envArgs = string.Join(" ", _config.EnvironmentVariables.Select(kv => $"-e {kv.Key}={kv.Value}"));

        var (exitCode, output) = await RunDockerAsync(
            $"run -d -p {HostPort}:{_config.ContainerPort} {envArgs} {_config.ImageName}",
            cancellationToken);

        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        _containerId = output.Trim();
        ConnectionString = _config.ConnectionStringFactory(HostPort);

        // Wait for database readiness with retries
        var ready = await WaitForReadyAsync(cancellationToken);
        if (!ready)
        {
            ConnectionString = null;
        }

        return ready;
    }

    /// <summary>
    /// Stops and removes the Docker container.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_containerId is not null)
        {
            await RunDockerAsync($"stop {_containerId}", CancellationToken.None);
            await RunDockerAsync($"rm -f {_containerId}", CancellationToken.None);
            _containerId = null;
        }
    }

    private async Task<bool> WaitForReadyAsync(CancellationToken cancellationToken)
    {
        const int maxRetries = 15;
        const int delayMs = 2000;

        for (var i = 0; i < maxRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if the container is still running (short-circuit if it crashed)
            var (psExit, psOutput) = await RunDockerAsync(
                $"ps -q --filter id={_containerId}",
                cancellationToken);

            if (psExit != 0 || string.IsNullOrWhiteSpace(psOutput))
            {
                return false;
            }

            var (exitCode, _) = await RunDockerAsync(
                $"exec {_containerId} {_config.HealthCheckCommand}",
                cancellationToken);

            if (exitCode == 0)
            {
                // Health check passed inside the container. Verify we can reach
                // the port from the host side (catches WSL2 forwarding delays).
                if (await VerifyHostPortAsync(cancellationToken))
                {
                    return true;
                }
            }

            await Task.Delay(delayMs, cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// Verifies the database port is reachable from the Windows host via TCP.
    /// Retries briefly to allow WSL2 port forwarding to stabilize.
    /// </summary>
    private async Task<bool> VerifyHostPortAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync("127.0.0.1", HostPort, cancellationToken);
                return true;
            }
            catch (SocketException)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }

        return false;
    }

    /// <summary>
    /// Detects Docker availability, trying native Docker first then Docker via WSL.
    /// The result is cached statically so detection only runs once per test session.
    /// </summary>
    private static async Task<bool> DetectDockerAsync(CancellationToken cancellationToken)
    {
        if (s_detectionDone)
        {
            return s_dockerFound;
        }

        // Try native Docker first (Linux, macOS, Docker Desktop on Windows)
        try
        {
            var (exitCode, _) = await RunProcessAsync("docker", "info", cancellationToken);
            if (exitCode == 0)
            {
                s_useWsl = false;
                s_dockerFound = true;
                s_detectionDone = true;
                return true;
            }
        }
        catch
        {
            // Native Docker not available
        }

        // Try Docker via WSL (Windows dev machines with Docker Engine in WSL 2)
        try
        {
            var (exitCode, _) = await RunProcessAsync("wsl", "docker info", cancellationToken);
            if (exitCode == 0)
            {
                s_useWsl = true;
                s_dockerFound = true;
                s_detectionDone = true;
                return true;
            }
        }
        catch
        {
            // WSL Docker not available either
        }

        s_detectionDone = true;
        return false;
    }

    /// <summary>
    /// Runs a Docker command, routing through WSL if native Docker is not available.
    /// </summary>
    private static Task<(int ExitCode, string Output)> RunDockerAsync(
        string arguments, CancellationToken cancellationToken)
    {
        return s_useWsl
            ? RunProcessAsync("wsl", $"docker {arguments}", cancellationToken)
            : RunProcessAsync("docker", arguments, cancellationToken);
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName, string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return (-1, string.Empty);
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, output);
    }
}
