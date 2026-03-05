using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Infrastructure;

[TestClass]
public class SystemdServiceHelperTests
{
    // ──────────────────────────────────────────────────────────────────────
    //  The #1 reason the service failed on Linux was Type=notify without
    //  sd_notify. These tests lock in the fix so it can never regress.
    // ──────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void GenerateUnitFile_UsesTypeForking_NotNotify()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);

        Assert.IsTrue(unit.Contains("Type=forking"), "Must use Type=forking");
        Assert.IsFalse(unit.Contains("Type=notify"), "Must NOT use Type=notify");
        Assert.IsFalse(unit.Contains("Type=simple"), "Must NOT use Type=simple");
    }

    [TestMethod]
    public void GenerateUnitFile_IncludesPIDFile()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);

        Assert.IsTrue(
            unit.Contains("PIDFile=/run/dotnetcloud/dotnetcloud.pid"),
            "PIDFile must point to /run/dotnetcloud/dotnetcloud.pid");
    }

    [TestMethod]
    public void GenerateUnitFile_IncludesRuntimeDirectory()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);

        // RuntimeDirectory=dotnetcloud tells systemd to create /run/dotnetcloud/
        // owned by the service user before ExecStart runs.
        Assert.IsTrue(
            unit.Contains("RuntimeDirectory=dotnetcloud"),
            "RuntimeDirectory=dotnetcloud must be present so systemd creates /run/dotnetcloud/");
    }

    [TestMethod]
    public void GenerateUnitFile_DoesNotContainExecStop()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);

        // With Type=forking + PIDFile, systemd sends SIGTERM to the main PID
        // on 'systemctl stop'. An ExecStop that calls 'dotnetcloud stop' would
        // race against systemd and send SIGKILL before the server can drain.
        Assert.IsFalse(
            unit.Contains("ExecStop="),
            "ExecStop must NOT be present — systemd handles SIGTERM natively with Type=forking");
    }

    [TestMethod]
    public void GenerateUnitFile_IncludesGuessMainPIDNo()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);

        Assert.IsTrue(
            unit.Contains("GuessMainPID=no"),
            "GuessMainPID=no prevents systemd from guessing — PIDFile is authoritative");
    }

    [TestMethod]
    public void GenerateUnitFile_ExecStart_UsesStartCommand()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);

        Assert.IsTrue(
            unit.Contains("ExecStart=/opt/dotnetcloud/dotnetcloud start"),
            "ExecStart must invoke 'dotnetcloud start'");
    }

    [TestMethod]
    public void GenerateUnitFile_IncludesRequiredEnvironmentVariables()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);

        Assert.IsTrue(unit.Contains("DOTNET_ENVIRONMENT=Production"));
        Assert.IsTrue(unit.Contains("DOTNETCLOUD_CONFIG_DIR=/etc/dotnetcloud"));
        Assert.IsTrue(unit.Contains("DOTNETCLOUD_DATA_DIR=/var/lib/dotnetcloud"));
        Assert.IsTrue(unit.Contains("DOTNETCLOUD_LOG_DIR=/var/log/dotnetcloud"));
    }

    [TestMethod]
    public void GenerateUnitFile_IncludesServiceUserAndGroup()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);

        Assert.IsTrue(unit.Contains("User=dotnetcloud"));
        Assert.IsTrue(unit.Contains("Group=dotnetcloud"));
    }

    [TestMethod]
    public void GenerateUnitFile_IncludesRestartPolicy()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);

        Assert.IsTrue(unit.Contains("Restart=on-failure"));
        Assert.IsTrue(unit.Contains("RestartSec=10"));
    }

    [TestMethod]
    public void GenerateUnitFile_NonHardened_DoesNotContainHardeningDirectives()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);

        Assert.IsFalse(unit.Contains("NoNewPrivileges="));
        Assert.IsFalse(unit.Contains("ProtectSystem="));
        Assert.IsFalse(unit.Contains("ProtectHome="));
        Assert.IsFalse(unit.Contains("PrivateTmp="));
    }

    [TestMethod]
    public void GenerateUnitFile_Hardened_IncludesAllHardeningDirectives()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: true);

        Assert.IsTrue(unit.Contains("NoNewPrivileges=true"));
        Assert.IsTrue(unit.Contains("ProtectSystem=strict"));
        Assert.IsTrue(unit.Contains("ProtectHome=true"));
        Assert.IsTrue(unit.Contains("PrivateTmp=true"));
        Assert.IsTrue(unit.Contains("ReadWritePaths="));
    }

    [TestMethod]
    public void GenerateUnitFile_Hardened_ReadWritePaths_IncludesAllRequiredDirs()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: true);

        // Extract the ReadWritePaths value
        var lines = unit.Split('\n', StringSplitOptions.None).Select(l => l.TrimEnd('\r')).ToArray();
        var rwpLine = lines.FirstOrDefault(l => l.StartsWith("ReadWritePaths="));

        Assert.IsNotNull(rwpLine, "ReadWritePaths line must exist in hardened mode");
        Assert.IsTrue(rwpLine.Contains("/var/lib/dotnetcloud"), "Must include data dir");
        Assert.IsTrue(rwpLine.Contains("/var/log/dotnetcloud"), "Must include log dir");
        Assert.IsTrue(rwpLine.Contains("/run/dotnetcloud"), "Must include run dir");
        Assert.IsTrue(rwpLine.Contains("/etc/dotnetcloud"), "Must include config dir");
    }

    [TestMethod]
    public void GenerateUnitFile_Hardened_StillIncludesCoreDirectives()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: true);

        // Hardened mode must not lose any of the base directives.
        Assert.IsTrue(unit.Contains("Type=forking"));
        Assert.IsTrue(unit.Contains("PIDFile=/run/dotnetcloud/dotnetcloud.pid"));
        Assert.IsTrue(unit.Contains("RuntimeDirectory=dotnetcloud"));
        Assert.IsTrue(unit.Contains("ExecStart=/opt/dotnetcloud/dotnetcloud start"));
        Assert.IsFalse(unit.Contains("ExecStop="));
    }

    [TestMethod]
    public void GenerateUnitFile_OutputIsValidSystemdFormat()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);
        var lines = unit.Split('\n', StringSplitOptions.None).Select(l => l.TrimEnd('\r')).ToArray();

        // Must have [Unit], [Service], [Install] sections
        Assert.IsTrue(lines.Any(l => l.Trim() == "[Unit]"), "Missing [Unit] section");
        Assert.IsTrue(lines.Any(l => l.Trim() == "[Service]"), "Missing [Service] section");
        Assert.IsTrue(lines.Any(l => l.Trim() == "[Install]"), "Missing [Install] section");

        // No lines should have leading spaces (systemd keys must start at column 0).
        // Comments and blank lines are OK.
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            Assert.IsFalse(
                line.StartsWith(' '),
                $"Line has leading spaces (invalid for systemd): '{line}'");
        }
    }

    [TestMethod]
    public void GenerateUnitFile_NoTrailingWhitespaceOnDirectiveLines()
    {
        var unit = SystemdServiceHelper.GenerateUnitFile(hardened: false);
        var lines = unit.Split('\n', StringSplitOptions.None).Select(l => l.TrimEnd('\r')).ToArray();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.TrimEnd();
            Assert.AreEqual(trimmed, line,
                $"Line has trailing whitespace: '{line}'");
        }
    }
}
