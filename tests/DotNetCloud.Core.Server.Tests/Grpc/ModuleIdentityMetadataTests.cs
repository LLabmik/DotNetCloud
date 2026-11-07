using DotNetCloud.Core.Grpc;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Behavioural coverage for the module transport identity (decision D2 in
/// <c>docs/THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md</c>). The wire names are asserted as string
/// literals on the metadata produced by production code, because header-name drift between the
/// producer and the core/host consumers is the failure mode the plan warns about.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ModuleIdentityMetadataTests
{
    private const string ExpectedModuleIdHeader = "dnc-module-id";
    private const string ExpectedModuleTokenHeader = "dnc-module-token";
    private const string ExpectedUserAssertionHeader = "dnc-user-assertion";

    [TestMethod]
    public void Create_WithTokenInEnvironment_AttachesModuleIdAndTokenUnderDncHeaderNames()
    {
        RunWithToken("token-value", () =>
        {
            var metadata = ModuleIdentityMetadata.Create("dotnetcloud.files");

            Assert.AreEqual("dotnetcloud.files", metadata.GetValue(ExpectedModuleIdHeader));
            Assert.AreEqual("token-value", metadata.GetValue(ExpectedModuleTokenHeader));
            Assert.IsNull(metadata.GetValue(ExpectedUserAssertionHeader));
        });
    }

    [TestMethod]
    public void Create_WithoutTokenInEnvironment_OmitsTokenHeader()
    {
        RunWithToken(null, () =>
        {
            var metadata = ModuleIdentityMetadata.Create("dotnetcloud.files");

            Assert.AreEqual("dotnetcloud.files", metadata.GetValue(ExpectedModuleIdHeader));
            Assert.IsNull(metadata.GetValue(ExpectedModuleTokenHeader));
        });
    }

    [TestMethod]
    public void Create_WithUserAssertion_AttachesAssertionUnderDncHeaderName()
    {
        RunWithToken(null, () =>
        {
            var metadata = ModuleIdentityMetadata.Create("dotnetcloud.files", "signed-assertion");

            Assert.AreEqual("signed-assertion", metadata.GetValue(ExpectedUserAssertionHeader));
        });
    }

    /// <summary>
    /// Runs <paramref name="action"/> with the supervisor-issued token variable set, restoring the
    /// previous value afterwards.
    /// </summary>
    private static void RunWithToken(string? token, Action action)
    {
        var previous = Environment.GetEnvironmentVariable(ModuleIdentityMetadata.ModuleTokenEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(ModuleIdentityMetadata.ModuleTokenEnvironmentVariable, token);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModuleIdentityMetadata.ModuleTokenEnvironmentVariable, previous);
        }
    }
}
