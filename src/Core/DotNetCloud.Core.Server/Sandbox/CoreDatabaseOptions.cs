using DotNetCloud.Core.Data.Naming;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// The core database the server is running against, captured at startup for services that need
/// raw ADO.NET access (module data-store provisioning, health probes).
/// </summary>
/// <param name="ConnectionString">The core application connection string.</param>
/// <param name="Provider">The configured database engine.</param>
public sealed record CoreDatabaseOptions(string ConnectionString, DatabaseProvider Provider);
