using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Infrastructure;

[TestClass]
public class ConsoleOutputTests
{
    [TestMethod]
    public void FormatStatus_Running_ReturnsGreenIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Running");
        Assert.IsTrue(result.Contains("Running"));
        Assert.IsTrue(result.Contains("●"));
    }

    [TestMethod]
    public void FormatStatus_Enabled_ReturnsGreenIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Enabled");
        Assert.IsTrue(result.Contains("Enabled"));
        Assert.IsTrue(result.Contains("●"));
    }

    [TestMethod]
    public void FormatStatus_Healthy_ReturnsGreenIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Healthy");
        Assert.IsTrue(result.Contains("Healthy"));
        Assert.IsTrue(result.Contains("●"));
    }

    [TestMethod]
    public void FormatStatus_Stopped_ReturnsGrayIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Stopped");
        Assert.IsTrue(result.Contains("Stopped"));
        Assert.IsTrue(result.Contains("○"));
    }

    [TestMethod]
    public void FormatStatus_Disabled_ReturnsGrayIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Disabled");
        Assert.IsTrue(result.Contains("Disabled"));
        Assert.IsTrue(result.Contains("○"));
    }

    [TestMethod]
    public void FormatStatus_Degraded_ReturnsYellowIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Degraded");
        Assert.IsTrue(result.Contains("Degraded"));
        Assert.IsTrue(result.Contains("●"));
    }

    [TestMethod]
    public void FormatStatus_Failed_ReturnsRedIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Failed");
        Assert.IsTrue(result.Contains("Failed"));
        Assert.IsTrue(result.Contains("●"));
    }

    [TestMethod]
    public void FormatStatus_Crashed_ReturnsRedIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Crashed");
        Assert.IsTrue(result.Contains("Crashed"));
        Assert.IsTrue(result.Contains("●"));
    }

    [TestMethod]
    public void FormatStatus_Starting_ReturnsTransitionIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Starting");
        Assert.IsTrue(result.Contains("Starting"));
        Assert.IsTrue(result.Contains("◐"));
    }

    [TestMethod]
    public void FormatStatus_Stopping_ReturnsTransitionIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Stopping");
        Assert.IsTrue(result.Contains("Stopping"));
        Assert.IsTrue(result.Contains("◐"));
    }

    [TestMethod]
    public void FormatStatus_Unknown_ReturnsFallback()
    {
        var result = ConsoleOutput.FormatStatus("SomeCustomStatus");
        Assert.IsTrue(result.Contains("SomeCustomStatus"));
    }

    [TestMethod]
    public void FormatStatus_IsCaseInsensitive()
    {
        var lower = ConsoleOutput.FormatStatus("running");
        var upper = ConsoleOutput.FormatStatus("RUNNING");
        var mixed = ConsoleOutput.FormatStatus("Running");

        // All should contain the green indicator
        Assert.IsTrue(lower.Contains("●"));
        Assert.IsTrue(upper.Contains("●"));
        Assert.IsTrue(mixed.Contains("●"));
    }

    [TestMethod]
    public void FormatStatus_Error_ReturnsRedIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Error");
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("●"));
    }

    [TestMethod]
    public void FormatStatus_Unhealthy_ReturnsRedIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Unhealthy");
        Assert.IsTrue(result.Contains("Unhealthy"));
        Assert.IsTrue(result.Contains("●"));
    }

    [TestMethod]
    public void FormatStatus_Warning_ReturnsYellowIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Warning");
        Assert.IsTrue(result.Contains("Warning"));
        Assert.IsTrue(result.Contains("●"));
    }

    [TestMethod]
    public void FormatStatus_Started_ReturnsGreenIndicator()
    {
        var result = ConsoleOutput.FormatStatus("Started");
        Assert.IsTrue(result.Contains("Started"));
        Assert.IsTrue(result.Contains("●"));
    }
}
