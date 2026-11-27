using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.ModulePackaging;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.CLI.Commands;

/// <summary>The <c>module dev</c> command: deploy a local build over an installed module.</summary>
/// <remarks>
/// <para>
/// The fast iteration loop for module authors: publish, swap the installed module's binaries, restart
/// it, and fail back to the previous build when the new one does not come up. Module identity —
/// schema, data directory, Data Protection ring, encrypted credentials — is preserved because nothing
/// but the binaries changes.
/// </para>
/// <para>
/// It also works for a module that was never installed: the build's <c>manifest.json</c> supplies the
/// identity, the isolated schema is provisioned and the manifest's setting defaults are seeded, so a
/// first run against a real user's data does not need a package and an install first.
/// </para>
/// <para>
/// It replaces module code without a signature or a review, so it is deliberately Development-only,
/// audited per deploy, and replaced builds are parked under <c>&lt;modules&gt;/.dev/</c> rather than the
/// rollback slot a catalogue install uses.
/// </para>
/// </remarks>
internal static class ModuleDevCommand
{
    private const string DevelopmentEnvironment = "Development";
    private static readonly string[] IgnoredPathSegments = ["bin", "obj", ".git", ".vs"];

    /// <summary>Creates the <c>dev</c> command.</summary>
    /// <returns>The configured command.</returns>
    public static Command Create()
    {
        var pathArgument = new Argument<string>("path")
        {
            Description = "The module host project (.csproj) or the directory that holds it",
        };
        var watchOption = new Option<bool>("--watch")
        {
            Description = "Republish and redeploy whenever a file in the module directory changes",
        };
        var configurationOption = new Option<string?>("--configuration")
        {
            Description = "Build configuration passed to 'dotnet publish' (default: Debug)",
        };
        var noPublishOption = new Option<bool>("--no-publish")
        {
            Description = "Reuse the existing publish output instead of building",
        };
        var allowSchemaChangeOption = new Option<bool>("--allow-schema-change")
        {
            Description = "Deploy a build whose migration assemblies changed since the last deploy",
        };
        var restoreOption = new Option<bool>("--restore")
        {
            Description = "Put the previous dev build back and exit",
        };

        var command = new Command("dev", "Deploy a local module build over the installed module, installing it first if needed (Development only)")
        {
            pathArgument,
            watchOption,
            configurationOption,
            noPublishOption,
            allowSchemaChangeOption,
            restoreOption,
        };

        command.SetAction(parseResult => RunAsync(
            parseResult.GetValue(pathArgument)!,
            parseResult.GetValue(watchOption),
            parseResult.GetValue(configurationOption) ?? "Debug",
            parseResult.GetValue(noPublishOption),
            parseResult.GetValue(allowSchemaChangeOption),
            parseResult.GetValue(restoreOption)));

        return command;
    }

    private static async Task<int> RunAsync(
        string path,
        bool watch,
        string buildConfiguration,
        bool noPublish,
        bool allowSchemaChange,
        bool restore)
    {
        var environmentReason = CheckEnvironment();
        if (environmentReason is not null)
        {
            ConsoleOutput.WriteError(environmentReason);
            return 1;
        }

        var modulesDirectory = ResolveModulesDirectory();
        if (modulesDirectory is null)
        {
            ConsoleOutput.WriteError(
                "The modules directory is not known to the CLI. Set DOTNETCLOUD_MODULES_DIR to the "
                + "server's modules directory (or make ProcessSupervisor:ModulesDirectory absolute in "
                + "the server's configuration) and try again.");
            return 1;
        }

        // The installer layout reads this first, so the service works on the server's directory even
        // though the CLI runs from somewhere else.
        Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULES_DIR", modulesDirectory);

        var module = ResolveModule(path, out var resolveError);
        if (module is null)
        {
            ConsoleOutput.WriteError(resolveError!);
            return 1;
        }

        var baseUrl = ResolveServerBaseUrl();
        var provider = ServiceProviderFactory.CreateForModuleDev(baseUrl);
        if (provider is null)
        {
            return 1;
        }

        await using var disposer = provider;

        using var scope = provider.CreateScope();
        var deployService = scope.ServiceProvider.GetRequiredService<IModuleDevDeployService>();
        var processControl = scope.ServiceProvider.GetRequiredService<IModuleDevProcessControl>();

        if (processControl is ReadinessModuleDevProcessControl readiness)
        {
            var serverReason = await readiness.CheckServerAsync();
            if (serverReason is not null)
            {
                ConsoleOutput.WriteError(
                    $"{serverReason} The supervisor owns module processes, so 'module dev' needs a "
                    + $"running server: start it with 'dotnetcloud service start'.");
                return 1;
            }
        }

        ConsoleOutput.WriteHeader("Module Development Deploy");
        ConsoleOutput.WriteDetail("Module", module.ModuleId);
        ConsoleOutput.WriteDetail("Project", module.HostProject);
        ConsoleOutput.WriteDetail("Directory", Path.Combine(modulesDirectory, module.ModuleId));

        if (restore)
        {
            var restored = await deployService.RestoreAsync(module.ModuleId);
            return Report(restored);
        }

        var publishDirectory = Path.Combine(modulesDirectory, ".dev", module.ModuleId, "publish");

        // A build that does not compile must leave the running module untouched, so the publish runs
        // before anything touches the installation.
        if (!await PublishAsync(module, publishDirectory, buildConfiguration, noPublish))
        {
            return 1;
        }

        var first = await DeployOnceAsync(deployService, module, publishDirectory, allowSchemaChange);
        if (first != 0 || !watch)
        {
            return first;
        }

        return await WatchAsync(module, publishDirectory, buildConfiguration, allowSchemaChange, deployService);
    }

    private static async Task<int> DeployOnceAsync(
        IModuleDevDeployService deployService,
        ResolvedModule module,
        string publishDirectory,
        bool allowSchemaChange)
    {
        var result = await deployService.DeployAsync(new ModuleDevDeployRequest
        {
            ModuleId = module.ModuleId,
            PublishedDirectory = publishDirectory,
            SourcePath = module.HostProject,
            AllowSchemaChange = allowSchemaChange,
        });

        return Report(result);
    }

    private static int Report(ModuleDevDeployResult result)
    {
        if (result.Success && !result.Deployed)
        {
            ConsoleOutput.WriteInfo("No change: the deployed build already matches this output.");
            return 0;
        }

        if (result.Success)
        {
            ConsoleOutput.WriteSuccess("Deployed and healthy.");
            return 0;
        }

        ConsoleOutput.WriteError(result.Error ?? "The deploy failed.");

        if (result.LogTail is { Length: > 0 } tail)
        {
            ConsoleOutput.WriteInfo("Module log tail:");
            Console.WriteLine(tail);
        }

        return 1;
    }

    private static async Task<int> WatchAsync(
        ResolvedModule module,
        string publishDirectory,
        string buildConfiguration,
        bool allowSchemaChange,
        IModuleDevDeployService deployService)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.CancelKeyPress += handler;
        try
        {
            using var signal = new SemaphoreSlim(0);

            void OnChanged(object? sender, FileSystemEventArgs eventArgs)
            {
                if (!IsIgnored(eventArgs.FullPath))
                {
                    signal.Release();
                }
            }

            using var watcher = new FileSystemWatcher(ResolveModuleDirectory(module))
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += (sender, eventArgs) => OnChanged(sender, eventArgs);
            watcher.EnableRaisingEvents = true;

            ConsoleOutput.WriteInfo("Watching for changes. Press Ctrl+C to stop.");

            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    await signal.WaitAsync(cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                // Debounce: editors write a file several times in a row.
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                while (signal.CurrentCount > 0)
                {
                    signal.Wait(0);
                }

                if (!await PublishAsync(module, publishDirectory, buildConfiguration, noPublish: false))
                {
                    ConsoleOutput.WriteWarning("Build failed; the running module was left alone.");
                    continue;
                }

                var result = await deployService.DeployAsync(new ModuleDevDeployRequest
                {
                    ModuleId = module.ModuleId,
                    PublishedDirectory = publishDirectory,
                    SourcePath = module.HostProject,
                    AllowSchemaChange = allowSchemaChange,
                });

                Report(result);
            }

            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private static bool IsIgnored(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => IgnoredPathSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));

    private static string? CheckEnvironment()
    {
        var configuration = ServiceProviderFactory.BuildCliConfiguration();
        var environment = configuration["ASPNETCORE_ENVIRONMENT"]
                          ?? configuration["DOTNET_ENVIRONMENT"]
                          ?? "Production";

        if (string.Equals(environment, DevelopmentEnvironment, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"'module dev' installs unsigned code while preserving the module's identity, so it only "
               + $"runs in Development; this environment is '{environment}'. Set "
               + $"ASPNETCORE_ENVIRONMENT={DevelopmentEnvironment} for the shell you develop in.";
    }

    private static string? ResolveModulesDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULES_DIR");
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = ServiceProviderFactory.BuildCliConfiguration()["ProcessSupervisor:ModulesDirectory"];
        }

        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        // A relative path means "relative to the server's install directory", which the CLI cannot
        // know; guessing AppContext.BaseDirectory would silently use the CLI's own folder.
        return Path.IsPathRooted(configured) ? Path.GetFullPath(configured) : null;
    }

    private static string ResolveServerBaseUrl()
    {
        var configuration = ServiceProviderFactory.BuildCliConfiguration();
        var configured = configuration["Server:BaseUrl"] ?? configuration["Store:PublicBaseUrl"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var port = ServiceProviderFactory.BuildCliConfiguration()["HttpPort"];
        return int.TryParse(port, out var parsed) && parsed > 0
            ? $"http://localhost:{parsed}"
            : "http://localhost:5080";
    }

    private static ResolvedModule? ResolveModule(string path, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Give the module host project or its directory.";
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        string hostProject;

        if (File.Exists(fullPath))
        {
            hostProject = fullPath;
        }
        else if (Directory.Exists(fullPath))
        {
            var candidates = Directory.GetFiles(fullPath, "*.Host.csproj", SearchOption.AllDirectories);
            if (candidates.Length != 1)
            {
                error = candidates.Length == 0
                    ? $"No module host project (*.Host.csproj) under '{fullPath}'."
                    : $"More than one host project under '{fullPath}'; pass the one to deploy:"
                      + Environment.NewLine + string.Join(Environment.NewLine, candidates.Select(candidate => "  " + candidate));
                return null;
            }

            hostProject = candidates[0];
        }
        else
        {
            error = $"'{fullPath}' does not exist.";
            return null;
        }

        var projectDirectory = Path.GetDirectoryName(hostProject)!;
        var manifestPath = new[]
            {
                Path.Combine(projectDirectory, "manifest.json"),
                Path.Combine(projectDirectory, "..", "manifest.json"),
            }
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);

        if (manifestPath is null)
        {
            error = $"No manifest.json next to '{hostProject}' or one level above it.";
            return null;
        }

        string moduleId;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            moduleId = document.RootElement.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException ex)
        {
            error = $"'{manifestPath}' is not valid JSON: {ex.Message}";
            return null;
        }

        if (moduleId.Length == 0)
        {
            error = $"'{manifestPath}' does not declare an id.";
            return null;
        }

        return new ResolvedModule(moduleId, hostProject, Path.GetDirectoryName(manifestPath)!);
    }

    private static string ResolveModuleDirectory(ResolvedModule module) =>
        File.Exists(module.HostProject)
            ? Directory.GetParent(module.HostProject)?.FullName ?? module.ModuleDirectory
            : module.ModuleDirectory;

    private static async Task<bool> PublishAsync(
        ResolvedModule module, string publishDirectory, string buildConfiguration, bool noPublish)
    {
        if (noPublish)
        {
            ConsoleOutput.WriteInfo("Reusing the existing publish output (--no-publish).");
            return true;
        }

        ConsoleOutput.WriteStep(1, 2, "dotnet publish");
        Directory.CreateDirectory(publishDirectory);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in new[]
                 {
                     "publish", module.HostProject,
                     "-c", buildConfiguration,
                     "--self-contained", "false",
                     "-o", publishDirectory,
                     "-nologo",
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            ConsoleOutput.WriteError("Could not start 'dotnet publish'.");
            return false;
        }

        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            ConsoleOutput.WriteError($"'dotnet publish' failed with exit code {process.ExitCode}.");
            Console.WriteLine(standardError);
            Console.WriteLine(standardOutput);
            return false;
        }

        return true;
    }

    private sealed record ResolvedModule(string ModuleId, string HostProject, string ModuleDirectory);
}
