namespace DotNetCloud.Core.Server.Tests.Modules;

/// <summary>
/// Guards the frame ticket against being re-minted on every shell render.
/// </summary>
/// <remarks>
/// The ticket is embedded in the iframe's <c>src</c>, so minting a new one on each parameter set
/// changed the attribute on every re-render (module health polls, navigation-registry changes) and
/// restarted the frame's navigation. Each restart aborted the in-flight load with <c>ERR_ABORTED</c>,
/// so a cold WebAssembly boot never finished and the shell showed "The app did not respond. It may be
/// stopped or restarting." for every module app and Home widget.
/// </remarks>
[TestClass]
public class ModuleAppHostTicketStabilityTests
{
    [TestMethod]
    public void FrameHost_MintsOneTicketPerModule()
    {
        var source = ReadSource("src/UI/DotNetCloud.UI.Web/Components/Modules/ModuleAppHost.razor.cs");

        StringAssert.Contains(
            source,
            "_ticketModuleId",
            "the ticket must be cached per module id; re-minting it on every parameter set restarts the frame navigation");
        StringAssert.Contains(
            source,
            "string.Equals(_ticketModuleId, ModuleId",
            "the guard must compare the module id, so switching modules still mints a fresh ticket");
    }

    [TestMethod]
    public void FrameMarkup_BindsTheIframeSourceToTheCachedTicket()
    {
        var markup = ReadSource("src/UI/DotNetCloud.UI.Web/Components/Modules/ModuleAppHost.razor");

        StringAssert.Contains(markup, "src=\"@Source\"", "the iframe loads the shell-composed source");
        StringAssert.Contains(
            markup,
            "@ref=\"_frame\"",
            "the host needs the element reference for the mount bridge");
    }

    [TestMethod]
    public void FrameHandshake_BothSidesRepeatUntilAnswered()
    {
        var host = ReadSource("src/UI/DotNetCloud.UI.Web/wwwroot/js/module-app-host.js");
        var bridge = ReadSource("src/SDK/DotNetCloud.Modules.Sdk/wwwroot/dncModuleBridge.js");

        // The host mounts the frame and registers its listener asynchronously; the frame boots
        // asynchronously too. Whichever side is late drops the other's single message, leaving the
        // shell on "Starting ..." until the handshake times out. Both sides must therefore repeat.
        StringAssert.Contains(
            host,
            "function beginHandshake()",
            "the host must be able to repeat dnc:init");
        StringAssert.Contains(
            host,
            "INIT_RETRY_MS",
            "the host repeats dnc:init on an interval instead of sending it once");
        StringAssert.Contains(
            bridge,
            "function announceReady()",
            "the frame must repeat dnc:ready until the host answers");
        StringAssert.Contains(
            bridge,
            "stopReadyRetries()",
            "repeated announcements must stop as soon as the host is heard from");
    }

    private static string ReadSource(string relativePath)
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(path), $"Expected source file was not found: {path}");
        return File.ReadAllText(path);
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
