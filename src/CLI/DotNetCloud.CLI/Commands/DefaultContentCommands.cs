using System.CommandLine;
using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.CLI.Commands;

/// <summary>
/// Default-content template commands: status, validate, reconcile, seed-defaults, sync-seed,
/// export and import.
/// </summary>
internal static class DefaultContentCommands
{
    /// <summary>Creates the <c>files</c> parent command.</summary>
    public static Command Create()
    {
        var command = new Command("files", "Manage the Files module");
        command.Subcommands.Add(CreateDefaultContent());
        return command;
    }

    private static Command CreateDefaultContent()
    {
        var command = new Command("default-content", "Manage the default user content template");
        command.Subcommands.Add(CreateStatus());
        command.Subcommands.Add(CreateValidate());
        command.Subcommands.Add(CreateReconcile());
        command.Subcommands.Add(CreateSeedDefaults());
        command.Subcommands.Add(CreateSyncSeed());
        command.Subcommands.Add(CreateExport());
        command.Subcommands.Add(CreateImport());
        return command;
    }

    private static Command CreateStatus()
    {
        var command = new Command("status", "Show the effective template state and provisioning queue");
        command.SetAction(async _ =>
        {
            using var provider = ServiceProviderFactory.CreateForFilesDefaultContent();
            if (provider is null)
                return 1;

            using var scope = provider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<DefaultUserContentStore>();
            var settings = scope.ServiceProvider.GetRequiredService<IDefaultUserContentSettingsProvider>();
            var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();

            var options = await settings.GetAsync();
            var validation = await store.ValidateAsync(options);
            var manifest = validation.Manifest;

            ConsoleOutput.WriteHeader("Default user content");
            Console.WriteLine($"Enabled:       {options.Enabled}");
            Console.WriteLine($"Template:      {manifest.RootPath}");
            Console.WriteLine($"Exists:        {validation.RootExists}");
            Console.WriteLine($"Readable:      {validation.Readable}");
            Console.WriteLine($"Contents:      {manifest.FolderCount} folder(s), {manifest.FileCount} file(s), {FormatSize(manifest.TotalBytes)}");
            Console.WriteLine($"Issues:        {validation.Issues.Count}");

            var rows = await db.UserContentProvisioning.AsNoTracking().ToListAsync();
            Console.WriteLine($"Users tracked: {rows.Count} " +
                              $"({rows.Count(r => r.Status == UserContentProvisioningStatus.Completed)} completed, " +
                              $"{rows.Count(r => r.Status == UserContentProvisioningStatus.Failed)} failed)");
            Console.WriteLine($"Pending:       {rows.Sum(r => r.PendingUpdates)} update(s), " +
                              $"{rows.Sum(r => r.PendingDeletions)} deletion(s)");

            return 0;
        });
        return command;
    }

    private static Command CreateValidate()
    {
        var command = new Command("validate", "Validate the template directory");
        command.SetAction(async _ =>
        {
            using var provider = ServiceProviderFactory.CreateForFilesDefaultContent();
            if (provider is null)
                return 1;

            using var scope = provider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<DefaultUserContentStore>();
            var settings = scope.ServiceProvider.GetRequiredService<IDefaultUserContentSettingsProvider>();
            var options = await settings.GetAsync();
            var validation = await store.ValidateAsync(options);

            Console.WriteLine($"Template: {validation.Manifest.RootPath}");

            if (validation.Issues.Count == 0)
            {
                Console.WriteLine("No issues found.");
                return 0;
            }

            foreach (var issue in validation.Issues)
            {
                Console.WriteLine($"[{issue.Severity}] {issue.RelativePath}: {issue.Message}");
            }

            return validation.IsValid ? 0 : 1;
        });
        return command;
    }

    private static Command CreateReconcile()
    {
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Preview without writing anything" };
        var allOption = new Option<bool>("--all") { Description = "Reconcile every user" };
        var userOption = new Option<string?>("--user") { Description = "Username to reconcile" };
        var publishOption = new Option<bool>("--publish-updates") { Description = "Replace unmodified copies of edited template files" };
        var overwriteOption = new Option<bool>("--overwrite-modified") { Description = "Replace user-modified copies" };
        var confirmDeletionsOption = new Option<bool>("--confirm-deletions") { Description = "Allow permanent deletion of copies of removed template items" };
        var confirmOverwritesOption = new Option<bool>("--confirm-overwrites") { Description = "Acknowledge overwriting user changes" };
        var waitOption = new Option<bool>("--wait") { Description = "Wait for the pass to finish (always on in CLI mode)" };

        var command = new Command("reconcile", "Run a convergence pass")
        {
            dryRunOption, allOption, userOption, publishOption, overwriteOption,
            confirmDeletionsOption, confirmOverwritesOption, waitOption
        };

        command.SetAction(async parse =>
        {
            var dryRun = parse.GetValue(dryRunOption);
            var all = parse.GetValue(allOption);
            var userName = parse.GetValue(userOption);
            var publish = parse.GetValue(publishOption);
            var overwrite = parse.GetValue(overwriteOption);
            var confirmDeletions = parse.GetValue(confirmDeletionsOption);
            var confirmOverwrites = parse.GetValue(confirmOverwritesOption);

            if (!all && string.IsNullOrWhiteSpace(userName))
            {
                ConsoleOutput.WriteError("Specify --all or --user <username>.");
                return 1;
            }

            if (overwrite && !confirmOverwrites)
            {
                ConsoleOutput.WriteError("--overwrite-modified requires --confirm-overwrites.");
                return 2;
            }

            using var provider = ServiceProviderFactory.CreateForFilesDefaultContent();
            if (provider is null)
                return 1;

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
            var coreDb = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var provisioner = scope.ServiceProvider.GetRequiredService<DefaultUserContentProvisioner>();

            var userIds = new List<Guid>();
            if (!string.IsNullOrWhiteSpace(userName))
            {
                var normalized = userName.Trim().ToLowerInvariant();
                var user = await coreDb.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserName!.ToLower() == normalized
                                              || u.DisplayName.ToLower() == normalized
                                              || u.Email!.ToLower() == normalized);

                if (user is null)
                {
                    ConsoleOutput.WriteError($"User '{userName}' not found.");
                    return 1;
                }

                userIds.Add(user.Id);
            }
            else
            {
                userIds = await coreDb.Users.AsNoTracking().Select(u => u.Id).ToListAsync();
            }

            var mode = ReconcileMode.AddOnly;
            if (publish)
                mode |= ReconcileMode.PublishUpdates;
            if (confirmDeletions)
                mode |= ReconcileMode.AllowDeletions;
            if (overwrite)
                mode |= ReconcileMode.OverwriteModified;

            if (dryRun)
            {
                foreach (var id in userIds)
                {
                    var preview = await provisioner.PreviewAsync(id);
                    Console.WriteLine(
                        $"{id}: add {preview.Adds}, update {preview.Updates}, overwrite {preview.Overwrites}, delete {preview.Deletes}");
                }

                if (!confirmDeletions && !publish && !overwrite)
                    Console.WriteLine("Dry run only — nothing was written.");

                return 0;
            }

            var totalCreated = 0;
            var totalSkipped = 0;
            var totalUpdates = 0;
            var totalDeletions = 0;

            foreach (var id in userIds)
            {
                var result = await provisioner.ReconcileAsync(id, mode);
                totalCreated += result.ItemsCreated;
                totalSkipped += result.ItemsSkipped;
                totalUpdates += result.PendingUpdates;
                totalDeletions += result.PendingDeletions;
            }

            Console.WriteLine(
                $"Reconciled {userIds.Count} user(s): {totalCreated} created, {totalSkipped} skipped, " +
                $"{totalUpdates} pending update(s), {totalDeletions} pending deletion(s).");

            if (totalDeletions > 0 && !confirmDeletions)
            {
                ConsoleOutput.WriteWarning(
                    "Deletions are pending. Re-run with --confirm-deletions to apply them.");
            }

            return 0;
        });

        return command;
    }

    private static Command CreateSeedDefaults()
    {
        var command = new Command("seed-defaults", "Add the stock folders to an empty template (additive only)");
        command.SetAction(async _ =>
        {
            using var provider = ServiceProviderFactory.CreateForFilesDefaultContent();
            if (provider is null)
                return 1;

            using var scope = provider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<DefaultUserContentStore>();
            var settings = scope.ServiceProvider.GetRequiredService<IDefaultUserContentSettingsProvider>();
            var created = await store.SeedDefaultFoldersAsync(await settings.GetAsync());
            Console.WriteLine($"Created {created} folder(s).");
            return 0;
        });
        return command;
    }

    private static Command CreateSyncSeed()
    {
        var command = new Command("sync-seed", "Copy the packaged seed tree into the template (additive only)");
        command.SetAction(_ =>
        {
            var seed = FindPackagedSeed();
            if (seed is null || !Directory.Exists(seed))
            {
                ConsoleOutput.WriteError("Packaged seed tree not found (expected 'seed/default-user-content' next to the binaries).");
                return Task.FromResult(1);
            }

            // Resolve the template root from the configured data directory when the CLI is not
            // running under the systemd unit (which sets DOTNETCLOUD_DATA_DIR itself).
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR")))
                Environment.SetEnvironmentVariable("DOTNETCLOUD_DATA_DIR", CliConfiguration.Load().DataDirectory);

            var options = new DefaultUserContentOptions();
            var root = DefaultUserContentStore.ResolveRootPath(options);
            Directory.CreateDirectory(root);

            var copied = 0;
            foreach (var source in Directory.EnumerateFiles(seed, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(seed, source);
                var destination = Path.Combine(root, relative);
                if (File.Exists(destination))
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination);
                copied++;
            }

            foreach (var directory in Directory.EnumerateDirectories(seed, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(root, Path.GetRelativePath(seed, directory)));
            }

            DefaultUserContentStore.EnsureMarker(root);
            Console.WriteLine($"Copied {copied} file(s) from the packaged seed into {root}.");
            return Task.FromResult(0);
        });
        return command;
    }

    private static Command CreateExport()
    {
        var pathArgument = new Argument<string>("path") { Description = "Destination ZIP path" };
        var command = new Command("export", "Export the template as a ZIP archive") { pathArgument };
        command.SetAction(async parse =>
        {
            using var provider = ServiceProviderFactory.CreateForFilesDefaultContent();
            if (provider is null)
                return 1;

            using var scope = provider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<DefaultUserContentStore>();
            var settings = scope.ServiceProvider.GetRequiredService<IDefaultUserContentSettingsProvider>();
            var path = parse.GetValue(pathArgument)!;

            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, System.IO.FileShare.None,
                bufferSize: 81920, useAsync: true);
            await store.ExportAsync(await settings.GetAsync(), stream);

            Console.WriteLine($"Exported template to {path}.");
            return 0;
        });
        return command;
    }

    private static Command CreateImport()
    {
        var pathArgument = new Argument<string>("path") { Description = "Source ZIP path" };
        var mergeOption = new Option<bool>("--merge") { Description = "Add missing entries; skip existing (default)" };
        var replaceOption = new Option<bool>("--replace") { Description = "Replace the whole template (requires --force)" };
        var overwriteOption = new Option<bool>("--overwrite") { Description = "With --merge, replace files whose path exists" };
        var forceOption = new Option<bool>("--force") { Description = "Required with --replace; keeps a dated backup" };
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Validate and report; write nothing" };

        var command = new Command("import", "Import a validated ZIP into the template")
        {
            pathArgument, mergeOption, replaceOption, overwriteOption, forceOption, dryRunOption
        };

        command.SetAction(async parse =>
        {
            var path = parse.GetValue(pathArgument)!;
            var replace = parse.GetValue(replaceOption);
            var overwrite = parse.GetValue(overwriteOption);
            var force = parse.GetValue(forceOption);
            var dryRun = parse.GetValue(dryRunOption);

            if (replace && !force)
            {
                ConsoleOutput.WriteError("--replace requires --force.");
                return 2;
            }

            using var provider = ServiceProviderFactory.CreateForFilesDefaultContent();
            if (provider is null)
                return 1;

            using var scope = provider.CreateScope();
            var archive = scope.ServiceProvider.GetRequiredService<DefaultUserContentArchive>();
            var settings = scope.ServiceProvider.GetRequiredService<IDefaultUserContentSettingsProvider>();

            var result = await archive.ImportAsync(
                path, await settings.GetAsync(), replace, overwrite, dryRun);

            if (!result.Success)
            {
                foreach (var issue in result.Issues)
                    ConsoleOutput.WriteError(issue);
                return result.Issues.Count == 0 ? 3 : 1;
            }

            Console.WriteLine(
                $"{(result.DryRun ? "Validated" : "Imported")}: {result.Added} added, {result.Updated} updated, " +
                $"{result.Skipped} skipped, {result.Removed} removed.");

            if (!result.DryRun)
            {
                Console.WriteLine("Run 'dotnetcloud files default-content reconcile --all --publish-updates' " +
                                  "to push changed content to users.");
            }

            return 0;
        });
        return command;
    }

    private static string? FindPackagedSeed()
    {
        var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "seed", "default-user-content"));
        if (Directory.Exists(candidate))
            return candidate;

        var installDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_INSTALL_DIR") ?? "/opt/dotnetcloud";
        candidate = Path.Combine(installDir, "seed", "default-user-content");
        return Directory.Exists(candidate) ? candidate : null;
    }

    private static string FormatSize(long bytes) =>
        bytes < 1024 ? $"{bytes} B"
        : bytes < 1024L * 1024 ? $"{bytes / 1024.0:F1} KB"
        : bytes < 1024L * 1024 * 1024 ? $"{bytes / (1024.0 * 1024):F1} MB"
        : $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
}
