namespace DotNetCloud.Modules.Email.Tests;

/// <summary>
/// Guards the framed Email app's service registrations.
/// </summary>
/// <remarks>
/// A Blazor WebAssembly frame resolves every dependency from its own <c>Program.cs</c>, so a service the
/// panels inject but the host never registers fails at render time with
/// <c>Cannot provide a value for property 'State' ... There is no registered service of type</c>, and the
/// frame shows "The Email module UI failed to load". Found live in a browser against a real deploy on
/// 2026-11-12: <c>EmailState</c> had no registration at all, so no Email panel could render.
/// </remarks>
[TestClass]
public class EmailFrameServicesTests
{
    [TestMethod]
    public void Program_RegistersEmailStateForThePanels()
    {
        var program = ReadProgram();

        StringAssert.Contains(
            program,
            "AddScoped<EmailState>()",
            "EmailPanelBase injects EmailState, so every Email panel fails to render without this registration.");
    }

    [TestMethod]
    public void Program_RegistersTheServicesEmailStateDependsOn()
    {
        var program = ReadProgram();

        StringAssert.Contains(program, "AddScoped<EmailApi>()");
        StringAssert.Contains(program, "AddSingleton<ModuleFrameBridge>()");
    }

    [TestMethod]
    public void Program_RegistersTheFrameBridgeBeforeAuthentication()
    {
        var program = ReadProgram();

        var bridgeIndex = program.IndexOf("AddSingleton<ModuleFrameBridge>()", StringComparison.Ordinal);
        var authIndex = program.IndexOf("ModuleFrameAuthenticationStateProvider", StringComparison.Ordinal);

        Assert.IsTrue(
            bridgeIndex >= 0 && authIndex > bridgeIndex,
            "the frame bridge must be registered before the authentication provider that reports through it");
    }

    private static string ReadProgram()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ui", "Program.cs");

        Assert.IsTrue(File.Exists(path), $"Expected the frame's Program.cs to be copied to the test output: {path}");
        return File.ReadAllText(path);
    }
}
