using DotNetCloud.Core.Grpc.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Guards the endpoint check in the module-side capability clients.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GrpcOrganizationDirectory"/> and <see cref="GrpcTeamDirectory"/> create their channel
/// lazily. Both guarded every call with <c>if (_client is null)</c> and then logged "the endpoint is
/// not set" — so the first call <em>always</em> short-circuited, the channel was never created, and
/// every sandboxed module saw an empty organization / team listing while the log blamed the
/// environment.
/// </para>
/// <para>
/// Found live (Tracks, 2026-11-11): "GetUserOrganizations for … short-circuited:
/// DOTNETCLOUD_CORE_ENDPOINT is not set" was logged a dozen times while the same host resolved user
/// directory entries through the very same variable, and the user was an active member of two
/// organizations. The guard must test the configured endpoint.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class GrpcCapabilityClientEndpointTests
{
    private const string EndpointVariable = "DOTNETCLOUD_CORE_ENDPOINT";

    private string? _originalEndpoint;

    [TestInitialize]
    public void SaveEndpoint() => _originalEndpoint = Environment.GetEnvironmentVariable(EndpointVariable);

    [TestCleanup]
    public void RestoreEndpoint() =>
        Environment.SetEnvironmentVariable(EndpointVariable, _originalEndpoint);

    [TestMethod]
    public async Task OrganizationDirectory_WithoutAnEndpoint_ReturnsEmptyAndSaysWhy()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, null);
        var logger = new CapturingLogger<GrpcOrganizationDirectory>();

        using var directory = new GrpcOrganizationDirectory(logger);
        var organizations = await directory.GetUserOrganizationsAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, organizations.Count);
        Assert.IsTrue(
            logger.Messages.Any(m => m.Contains("short-circuited", StringComparison.OrdinalIgnoreCase)),
            "an unreachable core must be reported, not silently ignored");
    }

    [TestMethod]
    public async Task OrganizationDirectory_WithAnEndpoint_DoesNotShortCircuitOnTheFirstCall()
    {
        // A closed loopback port: the call fails fast, but it must *attempt* the call. Before the
        // fix every call short-circuited and this message appeared instead of a connection error.
        Environment.SetEnvironmentVariable(EndpointVariable, "http://127.0.0.1:1");
        var logger = new CapturingLogger<GrpcOrganizationDirectory>();

        using var directory = new GrpcOrganizationDirectory(logger);
        var organizations = await directory.GetUserOrganizationsAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, organizations.Count, "a failed call still degrades to an empty listing");
        Assert.IsFalse(
            logger.Messages.Any(m => m.Contains("short-circuited", StringComparison.OrdinalIgnoreCase)),
            "with an endpoint configured the client must call Core.Server, not short-circuit: " +
            string.Join(" | ", logger.Messages));
    }

    [TestMethod]
    public async Task TeamDirectory_WithAnEndpoint_DoesNotShortCircuitOnTheFirstCall()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "http://127.0.0.1:1");
        var logger = new CapturingLogger<GrpcTeamDirectory>();

        using var directory = new GrpcTeamDirectory(logger);
        var teams = await directory.GetTeamsForUserAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, teams.Count);
        Assert.IsFalse(
            logger.Messages.Any(m => m.Contains("short-circuited", StringComparison.OrdinalIgnoreCase)),
            "with an endpoint configured the client must call Core.Server, not short-circuit: " +
            string.Join(" | ", logger.Messages));
    }

    [TestMethod]
    public void IsConsentRequired_OnlyMatchesTheConsentTrailer()
    {
        var consentMetadata = new Metadata { { "error-code", "MODULE_CONSENT_REQUIRED" } };
        var consent = new RpcException(
            new Status(StatusCode.PermissionDenied, "consent needed"), consentMetadata);

        var plainDenial = new RpcException(
            new Status(StatusCode.PermissionDenied, "no grant"));

        Assert.IsTrue(GrpcOrganizationDirectory.IsConsentRequired(consent));
        Assert.IsFalse(GrpcOrganizationDirectory.IsConsentRequired(plainDenial));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
