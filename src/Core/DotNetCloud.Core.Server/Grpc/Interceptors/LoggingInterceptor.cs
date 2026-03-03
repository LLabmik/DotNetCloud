using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// gRPC interceptor that logs incoming and outgoing gRPC calls
/// with structured logging including module identity and timing.
/// </summary>
internal sealed class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var moduleId = context.UserState.TryGetValue("ModuleId", out var mid)
            ? mid as string ?? "unknown"
            : "unknown";

        _logger.LogDebug(
            "gRPC call started: {Method} from module {ModuleId}",
            context.Method, moduleId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await continuation(request, context);
            stopwatch.Stop();

            _logger.LogInformation(
                "gRPC call completed: {Method} from {ModuleId} in {ElapsedMs}ms",
                context.Method, moduleId, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (RpcException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                "gRPC call failed: {Method} from {ModuleId} in {ElapsedMs}ms - {StatusCode}: {Detail}",
                context.Method, moduleId, stopwatch.ElapsedMilliseconds,
                ex.StatusCode, ex.Status.Detail);

            throw;
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var moduleId = context.UserState.TryGetValue("ModuleId", out var mid)
            ? mid as string ?? "unknown"
            : "unknown";

        _logger.LogDebug(
            "gRPC stream started: {Method} from module {ModuleId}",
            context.Method, moduleId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await continuation(request, responseStream, context);
            stopwatch.Stop();

            _logger.LogInformation(
                "gRPC stream completed: {Method} from {ModuleId} in {ElapsedMs}ms",
                context.Method, moduleId, stopwatch.ElapsedMilliseconds);
        }
        catch (RpcException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                "gRPC stream failed: {Method} from {ModuleId} in {ElapsedMs}ms - {StatusCode}",
                context.Method, moduleId, stopwatch.ElapsedMilliseconds, ex.StatusCode);

            throw;
        }
    }
}
