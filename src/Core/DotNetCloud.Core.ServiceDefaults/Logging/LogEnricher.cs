using Serilog;
using Serilog.Context;
using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Core.ServiceDefaults.Logging;

/// <summary>
/// Provides enrichment for Serilog logs with contextual information.
/// </summary>
public static class LogEnricher
{
    /// <summary>
    /// Enriches the log context with user ID information.
    /// </summary>
    /// <param name="userId">The user ID to add to the log context.</param>
    /// <returns>A disposable object that removes the enrichment when disposed.</returns>
    public static IDisposable WithUserId(Guid userId)
    {
        return LogContext.PushProperty("UserId", userId);
    }

    /// <summary>
    /// Enriches the log context with request ID information.
    /// </summary>
    /// <param name="requestId">The request ID to add to the log context.</param>
    /// <returns>A disposable object that removes the enrichment when disposed.</returns>
    public static IDisposable WithRequestId(string requestId)
    {
        return LogContext.PushProperty("RequestId", requestId);
    }

    /// <summary>
    /// Enriches the log context with module name information.
    /// </summary>
    /// <param name="moduleName">The module name to add to the log context.</param>
    /// <returns>A disposable object that removes the enrichment when disposed.</returns>
    public static IDisposable WithModuleName(string moduleName)
    {
        return LogContext.PushProperty("ModuleName", moduleName);
    }

    /// <summary>
    /// Enriches the log context with operation name information.
    /// </summary>
    /// <param name="operationName">The operation name to add to the log context.</param>
    /// <returns>A disposable object that removes the enrichment when disposed.</returns>
    public static IDisposable WithOperationName(string operationName)
    {
        return LogContext.PushProperty("OperationName", operationName);
    }

    /// <summary>
    /// Enriches the log context with caller context information.
    /// </summary>
    /// <param name="context">The caller context to add to the log context.</param>
    /// <returns>A disposable object that removes the enrichment when disposed.</returns>
    public static IDisposable WithCallerContext(CallerContext context)
    {
        return LogContext.PushProperty("CallerContext", new
        {
            context.UserId,
            context.Type,
            Roles = string.Join(",", context.Roles)
        }, destructureObjects: true);
    }
}
