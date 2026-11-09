using DotNetCloud.Core.Grpc;

namespace DotNetCloud.Modules.Sdk;

/// <summary>
/// The runtime identity and endpoints a module host is launched with.
/// </summary>
/// <remarks>
/// The supervisor passes the module id and a per-launch credential in the environment. The
/// credential is the only proof of identity the core accepts, so it is read from the environment on
/// every call rather than cached at startup — a rotated credential takes effect without a restart
/// decision here.
/// </remarks>
public sealed class ModuleEnvironment
{
    /// <summary>Environment variable carrying the module id.</summary>
    public const string ModuleIdVariable = "DOTNETCLOUD_MODULE_ID";

    /// <summary>Environment variable carrying the module's core endpoint (gRPC).</summary>
    public const string CoreEndpointVariable = "DOTNETCLOUD_CORE_ENDPOINT";

    /// <summary>Environment variable carrying the module's scoped database connection string.</summary>
    public const string DatabaseConnectionStringVariable = "DOTNETCLOUD_MODULE_DB_CONNECTION_STRING";

    /// <summary>Environment variable carrying the core REST facade base URL.</summary>
    public const string CoreRestEndpointVariable = ModuleIdentityMetadata.CoreRestEndpointEnvironmentVariable;

    /// <summary>Initializes a new instance of the <see cref="ModuleEnvironment"/> class.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="coreEndpoint">The core gRPC endpoint.</param>
    /// <param name="databaseConnectionString">The scoped connection string, when the module has a data store.</param>
    /// <param name="coreRestEndpoint">The core REST facade base URL, when available.</param>
    public ModuleEnvironment(
        string moduleId,
        string coreEndpoint,
        string? databaseConnectionString = null,
        string? coreRestEndpoint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(coreEndpoint);

        ModuleId = moduleId;
        CoreEndpoint = coreEndpoint;
        DatabaseConnectionString = databaseConnectionString;
        CoreRestEndpoint = coreRestEndpoint;
    }

    /// <summary>Gets the module id the supervisor launched.</summary>
    public string ModuleId { get; }

    /// <summary>Gets the core gRPC endpoint.</summary>
    public string CoreEndpoint { get; }

    /// <summary>Gets the scoped database connection string, or <see langword="null"/> for <c>dataStore: none</c>.</summary>
    public string? DatabaseConnectionString { get; }

    /// <summary>Gets the core REST facade base URL, or <see langword="null"/> when it is not configured.</summary>
    public string? CoreRestEndpoint { get; }

    /// <summary>
    /// Reads the environment the supervisor provides.
    /// </summary>
    /// <returns>The module environment.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the module id or core endpoint is missing, which means the host was not launched
    /// by the supervisor.
    /// </exception>
    public static ModuleEnvironment FromEnvironment()
    {
        var moduleId = ReadRequired(ModuleIdVariable);
        var coreEndpoint = ReadRequired(CoreEndpointVariable);

        return new ModuleEnvironment(
            moduleId,
            coreEndpoint,
            ReadOptional(DatabaseConnectionStringVariable),
            ReadOptional(CoreRestEndpointVariable));
    }

    /// <summary>
    /// Reads the environment the supervisor provides, returning <see langword="false"/> when the
    /// host was not launched by the supervisor (local development, unit tests).
    /// </summary>
    /// <param name="environment">The environment, when the required variables are present.</param>
    /// <returns><see langword="true"/> when the environment is complete.</returns>
    public static bool TryFromEnvironment(out ModuleEnvironment? environment)
    {
        environment = null;

        var moduleId = ReadOptional(ModuleIdVariable);
        var coreEndpoint = ReadOptional(CoreEndpointVariable);

        if (moduleId is null || coreEndpoint is null)
        {
            return false;
        }

        environment = new ModuleEnvironment(
            moduleId,
            coreEndpoint,
            ReadOptional(DatabaseConnectionStringVariable),
            ReadOptional(CoreRestEndpointVariable));

        return true;
    }

    /// <summary>
    /// Builds the credential metadata for a core call.
    /// </summary>
    /// <param name="userAssertion">
    /// An explicit end-user assertion; when omitted the ambient assertion for the current request
    /// is attached automatically.
    /// </param>
    /// <returns>The gRPC metadata.</returns>
    public global::Grpc.Core.Metadata CreateMetadata(string? userAssertion = null) =>
        ModuleIdentityMetadata.Create(ModuleId, userAssertion);

    private static string ReadRequired(string name) =>
        ReadOptional(name) ?? throw new InvalidOperationException(
            $"Environment variable {name} is not set. The module host must be launched by the DotNetCloud supervisor.");

    private static string? ReadOptional(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
