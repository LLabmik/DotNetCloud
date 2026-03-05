using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Detects firewall state and offers to open ports on Linux (ufw).
/// Shows network guidance for LAN and internet access.
/// </summary>
internal static class FirewallHelper
{
    /// <summary>
    /// Returns <c>true</c> if ufw is installed and active.
    /// </summary>
    public static bool IsUfwActive()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo("ufw", "status")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Contains("Status: active", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Opens a TCP port in ufw.
    /// </summary>
    public static bool AllowPort(int port)
    {
        return RunCommand("ufw", $"allow {port}/tcp");
    }

    /// <summary>
    /// Gets the primary non-loopback IPv4 address of this machine,
    /// or <c>null</c> if none found.
    /// </summary>
    public static string? GetLanIpAddress()
    {
        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                    or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                foreach (var addr in iface.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(addr.Address))
                    {
                        return addr.Address.ToString();
                    }
                }
            }
        }
        catch
        {
            // Best-effort.
        }

        return null;
    }

    /// <summary>
    /// Writes firewall and network access guidance to the console.
    /// Offers to open ufw ports if ufw is active.
    /// </summary>
    public static void ShowNetworkGuidance(int httpPort, int? httpsPort, bool enableHttps)
    {
        var lanIp = GetLanIpAddress();
        var primaryPort = enableHttps && httpsPort.HasValue ? httpsPort.Value : httpPort;
        var scheme = enableHttps ? "https" : "http";

        Console.WriteLine();
        ConsoleOutput.WriteHeader("Network Access");

        // LAN access
        if (lanIp is not null)
        {
            ConsoleOutput.WriteInfo("Local network (LAN) access:");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"    {scheme}://{lanIp}:{primaryPort}");
            Console.ResetColor();
        }
        else
        {
            ConsoleOutput.WriteInfo("Could not detect LAN IP. Use your server's IP address.");
        }

        // Firewall
        if (IsUfwActive())
        {
            Console.WriteLine();
            ConsoleOutput.WriteInfo("Firewall (ufw) is active on this system.");

            if (ConsoleOutput.PromptConfirm(
                $"Open port {primaryPort} in the firewall so other devices can connect?",
                defaultValue: true))
            {
                if (AllowPort(primaryPort))
                {
                    ConsoleOutput.WriteSuccess($"Firewall rule added: port {primaryPort}/tcp allowed.");
                }
                else
                {
                    ConsoleOutput.WriteWarning($"Could not add firewall rule. Run manually:");
                    Console.WriteLine($"    sudo ufw allow {primaryPort}/tcp");
                }

                // Also open HTTP port if HTTPS is enabled (for redirect)
                if (enableHttps && httpPort != primaryPort)
                {
                    AllowPort(httpPort);
                }
            }
            else
            {
                ConsoleOutput.WriteInfo("You can open the port later:");
                Console.WriteLine($"    sudo ufw allow {primaryPort}/tcp");
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Console.WriteLine();
            ConsoleOutput.WriteInfo("No ufw firewall detected. If you use iptables or another firewall,");
            ConsoleOutput.WriteInfo($"ensure port {primaryPort}/tcp is open for incoming connections.");
        }

        // Internet access guidance
        Console.WriteLine();
        ConsoleOutput.WriteInfo("Internet access (outside your local network):");
        ConsoleOutput.WriteInfo("  Your router/firewall must forward external traffic to this server.");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("    IPv4 (most home/office networks):");
        Console.WriteLine($"      Set up port forwarding (NAT) on your router:");
        Console.WriteLine($"      External port {primaryPort} → Internal {lanIp ?? "<this server's IP>"}:{primaryPort}");
        Console.WriteLine("      Search: \"<your router brand> port forwarding\" for instructions.");
        Console.WriteLine();
        Console.WriteLine("    IPv6 (if your ISP supports it):");
        Console.WriteLine($"      Allow port {primaryPort}/tcp in your router's IPv6 firewall.");
        Console.WriteLine("      No NAT needed — IPv6 devices are directly addressable.");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static bool RunCommand(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
