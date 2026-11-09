using System.Text.RegularExpressions;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Architecture guard: a module host must reach other modules through the core, never directly.
/// </summary>
/// <remarks>
/// <para>
/// The rule (repo instructions, §"gRPC-Only Inter-Module Communication") is that every module talks
/// to the core and nothing else. A violation is easy to miss in review because it compiles, runs and
/// works on a single-machine deployment — the old Email module carried a
/// <c>FilesService.FilesServiceClient</c> built from <c>FilesGrpc:Address</c> for months, bypassing the
/// capability and consent gates entirely, and no test noticed.
/// </para>
/// <para>
/// The contract test applies to the modules already migrated by Track C and is extended as each one is
/// migrated. Today <c>Calendar</c> dials the Contacts module for attendee autocomplete; that is a real
/// violation, recorded for its own migration rather than fixed here (the fix is a brokered contacts
/// capability, exactly like <c>IFileDirectory</c>).
/// </para>
/// </remarks>
[TestClass]
public class ModuleBoundaryTests
{
    /// <summary>Modules whose hosts have been migrated by Track C.</summary>
    private static readonly string[] MigratedModules = ["Email", "Bookmarks", "Example"];

    [TestMethod]
    public void MigratedModuleHosts_DoNotImportAnotherModulesContract()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var offenders = new List<string>();

        foreach (var module in MigratedModules)
        {
            var moduleRoot = Path.Combine(root, "src", "Modules", module);

            if (!Directory.Exists(moduleRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(moduleRoot, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);

                foreach (Match match in Regex.Matches(text, @"using\s+DotNetCloud\.Modules\.(\w+)\.Host\.Protos;"))
                {
                    var importedModule = match.Groups[1].Value;

                    if (!string.Equals(importedModule, module, StringComparison.OrdinalIgnoreCase))
                    {
                        offenders.Add(
                            $"{Path.GetRelativePath(root, file)} imports {importedModule}'s host contract");
                    }
                }
            }
        }

        Assert.AreEqual(
            0,
            offenders.Count,
            "A migrated module host must reach other modules through the core (IDncModuleApi):\n"
                + string.Join('\n', offenders));
    }

    [TestMethod]
    public void ModuleProjects_ReferenceOnlyTheirOwnHost()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var moduleRoot = Path.Combine(root, "src", "Modules");
        var hosts = Directory.EnumerateFiles(moduleRoot, "*.Host.csproj", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path) ?? path,
                path => path,
                StringComparer.OrdinalIgnoreCase);

        var offenders = new List<string>();

        foreach (var (name, path) in hosts)
        {
            var text = File.ReadAllText(path);

            foreach (var other in hosts.Keys.Where(other => !string.Equals(other, name, StringComparison.OrdinalIgnoreCase)))
            {
                var projectName = other[..^".Host".Length];

                if (text.Contains(projectName + ".Host.csproj", StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{Path.GetRelativePath(root, path)} references {projectName}.Host");
                }
            }
        }

        Assert.AreEqual(
            0,
            offenders.Count,
            "Hosts must not reference each other; move the shared code into a contract or a core capability:\n"
                + string.Join('\n', offenders));
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DotNetCloud.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
