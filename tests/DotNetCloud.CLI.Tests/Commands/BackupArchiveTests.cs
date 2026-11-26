using DotNetCloud.CLI.Commands;
using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Commands;

/// <summary>
/// Tests for the backup payload definition, connection-string handling and safe restore-path
/// resolution. These are the pieces that make a cold restore possible: the database password has to
/// reach <c>pg_dump</c>/<c>psql</c>, and system configuration has to be in the archive.
/// </summary>
[TestClass]
public class BackupArchiveTests
{
    private const string PostgreSqlConnectionString =
        "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=S3cret-Pass;Port=5433";

    [TestMethod]
    public void TryGetConnectionStringPassword_PostgreSqlConnectionString_ReturnsPassword()
    {
        Assert.AreEqual("S3cret-Pass", BackupArchive.TryGetConnectionStringPassword(PostgreSqlConnectionString));
    }

    [TestMethod]
    public void TryGetConnectionStringPassword_PwdAlias_ReturnsPassword()
    {
        Assert.AreEqual("abc", BackupArchive.TryGetConnectionStringPassword("Host=x;Pwd=abc"));
    }

    [TestMethod]
    public void TryGetConnectionStringPassword_NoPassword_ReturnsNull()
    {
        Assert.IsNull(BackupArchive.TryGetConnectionStringPassword("Host=x;Username=y"));
    }

    [TestMethod]
    public void TryGetConnectionStringPassword_NullConnectionString_ReturnsNull()
    {
        Assert.IsNull(BackupArchive.TryGetConnectionStringPassword(null));
    }

    [TestMethod]
    public void BuildToolEnvironment_PostgreSql_ContainsPgPassword()
    {
        var environment = BackupArchive.BuildToolEnvironment("PostgreSQL", PostgreSqlConnectionString);

        Assert.AreEqual("S3cret-Pass", environment["PGPASSWORD"]);
    }

    [TestMethod]
    public void BuildToolEnvironment_SqlServer_DoesNotSetPgPassword()
    {
        var environment = BackupArchive.BuildToolEnvironment(
            "SqlServer", "Server=localhost;User Id=sa;Password=abc");

        Assert.AreEqual(0, environment.Count);
    }

    [TestMethod]
    public void IsPostgreSql_ProviderAliases_Match()
    {
        Assert.IsTrue(BackupArchive.IsPostgreSql("PostgreSQL"));
        Assert.IsTrue(BackupArchive.IsPostgreSql("postgres"));
        Assert.IsTrue(BackupArchive.IsPostgreSql("Npgsql"));
        Assert.IsFalse(BackupArchive.IsPostgreSql("SqlServer"));
    }

    [TestMethod]
    public void GetSystemPayloadEntries_ContainsEverythingNeededForColdRestore()
    {
        var entries = BackupArchive.GetSystemPayloadEntries(
            configDirectory: "/etc/dotnetcloud",
            installDirectory: "/opt/dotnetcloud",
            systemdDirectory: "/etc/systemd/system",
            coolwsdDirectory: "/etc/coolwsd",
            cliWrapperPath: "/usr/local/bin/dotnetcloud").Select(e => e.EntryName).ToList();

        CollectionAssert.Contains(entries, "config/config.json");
        CollectionAssert.Contains(entries, "config/env");
        CollectionAssert.Contains(entries, "config/certs/dotnetcloud-selfsigned.pfx");
        CollectionAssert.Contains(entries, "config/certs/dotnetcloud-selfsigned.pem");
        CollectionAssert.Contains(entries, "config/certs/dotnetcloud-root-ca.crt");
        CollectionAssert.Contains(entries, "system/systemd/dotnetcloud.service");
        CollectionAssert.Contains(entries, "system/bin/dotnetcloud");
        CollectionAssert.Contains(entries, "system/coolwsd/coolwsd.xml");
        CollectionAssert.Contains(entries, "opt/appsettings.json");
        CollectionAssert.Contains(entries, "opt/VERSION");
    }

    [TestMethod]
    public void GetSystemPayloadEntries_DoesNotIncludeBinariesOrLogs()
    {
        var entries = BackupArchive.GetSystemPayloadEntries().Select(e => e.EntryName).ToList();

        Assert.IsFalse(entries.Any(e => e.StartsWith("opt/server/", StringComparison.Ordinal)));
        Assert.IsFalse(entries.Any(e => e.StartsWith("opt/modules/", StringComparison.Ordinal)));
        Assert.IsFalse(entries.Any(e => e.Contains("logs/", StringComparison.Ordinal)));
        Assert.IsFalse(entries.Any(e => e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void TryResolveRestoreTarget_NormalRelativePath_ResolvesUnderRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "dnc-restore-test");

        var safe = BackupArchive.TryResolveRestoreTarget(root, "storage/files/ab/cd", out var target, out var reason);

        Assert.IsTrue(safe, reason);
        Assert.AreEqual(Path.GetFullPath(Path.Combine(root, "storage", "files", "ab", "cd")), target);
    }

    [TestMethod]
    public void TryResolveRestoreTarget_ParentTraversal_IsRejected()
    {
        var safe = BackupArchive.TryResolveRestoreTarget("/var/lib/dotnetcloud", "../../etc/passwd", out _, out var reason);

        Assert.IsFalse(safe);
        Assert.AreEqual("path traversal", reason);
    }

    [TestMethod]
    public void TryResolveRestoreTarget_AbsolutePath_IsRejected()
    {
        var safe = BackupArchive.TryResolveRestoreTarget("/var/lib/dotnetcloud", "/etc/passwd", out _, out var reason);

        Assert.IsFalse(safe);
        Assert.AreEqual("absolute path", reason);
    }

    [TestMethod]
    public void TryResolveRestoreTarget_WindowsDriveQualifiedPath_IsRejected()
    {
        var safe = BackupArchive.TryResolveRestoreTarget("/var/lib/dotnetcloud", @"C:\Windows\system32", out _, out var reason);

        Assert.IsFalse(safe);
        Assert.AreEqual("absolute path", reason);
    }

    [TestMethod]
    public void SerializeInfo_DeserializeInfo_RoundTripsAllFields()
    {
        var original = new BackupInfo
        {
            CreatedUtc = "2026-11-03T18:00:00Z",
            CliVersion = "0.6.16",
            DatabaseProvider = "PostgreSQL",
            DatabaseDumpIncluded = true,
            StorageManifestIncluded = true,
            DataDirectory = "/var/lib/dotnetcloud",
            ConfigDirectory = "/etc/dotnetcloud",
            ServiceUser = "dotnetcloud",
            SystemEntries = ["config/config.json", "config/certs/dotnetcloud-selfsigned.pfx"],
            DataFileCount = 195,
            EmptyDirectoryCount = 7,
            TotalEntryCount = 220,
            Warnings = ["example"],
        };

        var restored = BackupArchive.DeserializeInfo(BackupArchive.SerializeInfo(original));

        Assert.IsNotNull(restored);
        Assert.AreEqual(original.FormatVersion, restored.FormatVersion);
        Assert.AreEqual(original.CreatedUtc, restored.CreatedUtc);
        Assert.AreEqual(original.CliVersion, restored.CliVersion);
        Assert.AreEqual(original.DatabaseProvider, restored.DatabaseProvider);
        Assert.IsTrue(restored.DatabaseDumpIncluded);
        Assert.IsTrue(restored.StorageManifestIncluded);
        Assert.AreEqual(original.DataDirectory, restored.DataDirectory);
        Assert.AreEqual(original.ConfigDirectory, restored.ConfigDirectory);
        Assert.AreEqual(original.ServiceUser, restored.ServiceUser);
        CollectionAssert.AreEqual(original.SystemEntries.ToArray(), restored.SystemEntries.ToArray());
        Assert.AreEqual(original.DataFileCount, restored.DataFileCount);
        Assert.AreEqual(original.EmptyDirectoryCount, restored.EmptyDirectoryCount);
        Assert.AreEqual(original.TotalEntryCount, restored.TotalEntryCount);
        CollectionAssert.AreEqual(original.Warnings.ToArray(), restored.Warnings.ToArray());
    }

    [TestMethod]
    public void DeserializeInfo_InvalidJson_ReturnsNull()
    {
        Assert.IsNull(BackupArchive.DeserializeInfo("not json"));
    }

    [TestMethod]
    public void GetCliVersion_ReturnsNonEmptyValue()
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(BackupArchive.GetCliVersion()));
    }

    [TestMethod]
    public void BuildPgDumpArgs_NamesOutputFileWithDashF_AndNoStrayPositional()
    {
        var args = BackupCommands.BuildPgDumpArgs(PostgreSqlConnectionString, "/tmp/out.sql");

        // Regression: the output path used to be appended after -Fp, which pg_dump rejects as an
        // extra command-line argument ("too many command-line arguments").
        Assert.AreEqual("-Fp", args[^3]);
        Assert.AreEqual("-f", args[^2]);
        Assert.AreEqual("/tmp/out.sql", args[^1]);
        CollectionAssert.DoesNotContain(args, "-Fp/out.sql");
    }

    [TestMethod]
    public void BuildPgDumpArgs_IncludesConnectionDetails()
    {
        var args = BackupCommands.BuildPgDumpArgs(PostgreSqlConnectionString, "/tmp/out.sql");

        CollectionAssert.Contains(args, "localhost");
        Assert.AreEqual("5433", args[args.IndexOf("-p") + 1]);
        Assert.AreEqual("dotnetcloud", args[args.IndexOf("-U") + 1]);
        Assert.AreEqual("dotnetcloud", args[args.IndexOf("-d") + 1]);
    }

    [TestMethod]
    public void BuildSqlServerDumpArgs_IncludesServerCredentials()
    {
        var args = BackupCommands.BuildSqlServerDumpArgs(
            "Server=dbhost;User Id=sa;Password=pw", "C:\\backup\\dnc.bak");

        Assert.AreEqual("dbhost", args[args.IndexOf("-S") + 1]);
        Assert.AreEqual("sa", args[args.IndexOf("-U") + 1]);
        Assert.AreEqual("pw", args[args.IndexOf("-P") + 1]);
        Assert.IsTrue(args[^1].Contains("BACKUP DATABASE", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SerializeInfo_WithModuleInventory_RoundTripsModuleMetadata()
    {
        var info = new BackupInfo
        {
            CreatedUtc = "2026-11-25T00:00:00Z",
            Modules =
            [
                new BackupModuleInfo
                {
                    Id = "dotnetcloud.tracks",
                    Version = "2.0.0",
                    PublisherId = "dotnetcloud-official",
                    SignedByKeyId = "store-2027",
                    TrustTier = "Bundled",
                    DataStoreMode = "Isolated",
                    IsRequired = false,
                },
            ],
        };

        var restored = BackupArchive.DeserializeInfo(BackupArchive.SerializeInfo(info));

        Assert.IsNotNull(restored);
        Assert.AreEqual(1, restored.Modules.Count);
        Assert.AreEqual("dotnetcloud.tracks", restored.Modules[0].Id);
        Assert.AreEqual("2.0.0", restored.Modules[0].Version);
        Assert.AreEqual("Isolated", restored.Modules[0].DataStoreMode);
        Assert.IsFalse(restored.Modules[0].IsRequired);
    }

    [TestMethod]
    public void DeserializeInfo_ArchiveWithoutModuleList_ReturnsEmptyInventory()
    {
        // Older archives (and the server-side admin backup) carry no module list; reading them must
        // stay backward compatible rather than throwing.
        var restored = BackupArchive.DeserializeInfo(
            "{\"formatVersion\":2,\"createdUtc\":\"2026-01-01T00:00:00Z\"}");

        Assert.IsNotNull(restored);
        Assert.AreEqual(0, restored.Modules.Count);
    }

    [TestMethod]
    public void FindMissingModules_OnlyInstalledModulePresent_ReportsAbsentModule()
    {
        var root = Path.Combine(Path.GetTempPath(), "dnc-modules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "dotnetcloud.files"));
        try
        {
            var missing = BackupArchive.FindMissingModules(
                [
                    new BackupModuleInfo { Id = "dotnetcloud.files", Version = "1.0.0", IsRequired = true },
                    new BackupModuleInfo { Id = "dotnetcloud.tracks", Version = "2.0.0" },
                ],
                root);

            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual("dotnetcloud.tracks", missing[0].Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void FindMissingModules_AllDirectoriesPresent_ReturnsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "dnc-modules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "dotnetcloud.tracks"));
        try
        {
            var missing = BackupArchive.FindMissingModules(
                [new BackupModuleInfo { Id = "dotnetcloud.tracks", Version = "2.0.0" }],
                root);

            Assert.AreEqual(0, missing.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void FormatModuleInstallCommand_WithVersion_PinsStoreVersion()
    {
        var command = BackupArchive.FormatModuleInstallCommand(
            new BackupModuleInfo { Id = "dotnetcloud.tracks", Version = "2.0.0" });

        Assert.AreEqual(
            "sudo dotnetcloud module install dotnetcloud.tracks --version 2.0.0 --from-store --yes",
            command);
    }

    [TestMethod]
    public void FormatModuleInstallCommand_WithoutVersion_OmitsVersionFlag()
    {
        var command = BackupArchive.FormatModuleInstallCommand(
            new BackupModuleInfo { Id = "dotnetcloud.tracks" });

        Assert.IsFalse(command.Contains("--version", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildModuleRestorePlanLines_MissingStoreAndRequired_ExplainsEachSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "dnc-modules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var lines = BackupCommands.BuildModuleRestorePlanLines(
                [
                    new BackupModuleInfo { Id = "dotnetcloud.files", Version = "1.0.0", IsRequired = true },
                    new BackupModuleInfo { Id = "dotnetcloud.tracks", Version = "2.0.0" },
                ],
                root);

            Assert.AreEqual(2, lines.Count);
            StringAssert.Contains(lines[0], "required dotnetcloud.files 1.0.0");
            StringAssert.Contains(lines[1], "store    dotnetcloud.tracks 2.0.0");
            StringAssert.Contains(
                lines[1],
                "dotnetcloud module install dotnetcloud.tracks --version 2.0.0 --from-store --yes");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void BuildModuleRestorePlanLines_AllHostsPresent_ReturnsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "dnc-modules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "dotnetcloud.tracks"));
        try
        {
            var lines = BackupCommands.BuildModuleRestorePlanLines(
                [new BackupModuleInfo { Id = "dotnetcloud.tracks", Version = "2.0.0" }],
                root);

            Assert.AreEqual(0, lines.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
