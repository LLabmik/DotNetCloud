using DotNetCloud.Modules.Sdk;
using DotNetCloud.Modules.Sdk.Hosting;

namespace DotNetCloud.Modules.Sdk.Tests;

/// <summary>
/// Tests for the SDK runtime pieces: environment discovery and the host lifecycle contract.
/// </summary>
[TestClass]
public class DncModuleHostTests
{
    [TestMethod]
    public void TryFromEnvironment_NotLaunchedBySupervisor_ReturnsFalse()
    {
        var previousId = Environment.GetEnvironmentVariable(ModuleEnvironment.ModuleIdVariable);
        var previousEndpoint = Environment.GetEnvironmentVariable(ModuleEnvironment.CoreEndpointVariable);

        try
        {
            Environment.SetEnvironmentVariable(ModuleEnvironment.ModuleIdVariable, null);
            Environment.SetEnvironmentVariable(ModuleEnvironment.CoreEndpointVariable, null);

            Assert.IsFalse(ModuleEnvironment.TryFromEnvironment(out var environment));
            Assert.IsNull(environment);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModuleEnvironment.ModuleIdVariable, previousId);
            Environment.SetEnvironmentVariable(ModuleEnvironment.CoreEndpointVariable, previousEndpoint);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void TryFromEnvironment_SupervisorVariables_AreRead()
    {
        var previousId = Environment.GetEnvironmentVariable(ModuleEnvironment.ModuleIdVariable);
        var previousEndpoint = Environment.GetEnvironmentVariable(ModuleEnvironment.CoreEndpointVariable);
        var previousDatabase = Environment.GetEnvironmentVariable(ModuleEnvironment.DatabaseConnectionStringVariable);

        try
        {
            Environment.SetEnvironmentVariable(ModuleEnvironment.ModuleIdVariable, "org.example.app");
            Environment.SetEnvironmentVariable(ModuleEnvironment.CoreEndpointVariable, "http://localhost:5100");
            Environment.SetEnvironmentVariable(
                ModuleEnvironment.DatabaseConnectionStringVariable, "Host=localhost;Database=app");

            Assert.IsTrue(ModuleEnvironment.TryFromEnvironment(out var environment));
            Assert.IsNotNull(environment);
            Assert.AreEqual("org.example.app", environment.ModuleId);
            Assert.AreEqual("http://localhost:5100", environment.CoreEndpoint);
            Assert.AreEqual("Host=localhost;Database=app", environment.DatabaseConnectionString);
            Assert.IsNull(environment.CoreRestEndpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModuleEnvironment.ModuleIdVariable, previousId);
            Environment.SetEnvironmentVariable(ModuleEnvironment.CoreEndpointVariable, previousEndpoint);
            Environment.SetEnvironmentVariable(ModuleEnvironment.DatabaseConnectionStringVariable, previousDatabase);
        }
    }

    [TestMethod]
    public void FromEnvironment_MissingSupervisorVariables_Throws()
    {
        var previousId = Environment.GetEnvironmentVariable(ModuleEnvironment.ModuleIdVariable);

        try
        {
            Environment.SetEnvironmentVariable(ModuleEnvironment.ModuleIdVariable, null);

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => ModuleEnvironment.FromEnvironment());
            StringAssert.Contains(ex.Message, ModuleEnvironment.ModuleIdVariable);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModuleEnvironment.ModuleIdVariable, previousId);
        }
    }

    [TestMethod]
    public async Task Start_WithoutEnvironment_FailsInsteadOfGuessing()
    {
        await using var host = new TestHost();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.StartAsync());
    }

    [TestMethod]
    public async Task Start_IsIdempotent_AndStopReleasesOnce()
    {
        await using var host = new TestHost();
        await host.InitializeAsync(new ModuleEnvironment("org.example.app", "http://localhost:5100"));

        await host.StartAsync();
        await host.StartAsync();
        await host.StopAsync();
        await host.StopAsync();

        Assert.AreEqual(1, host.StartCount);
        Assert.AreEqual(1, host.StopCount);
        Assert.IsFalse(host.IsStarted);
    }

    private sealed class TestHost : DncModuleHost
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }
}
