using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// gRPC interceptor that creates distributed tracing spans for incoming calls.
/// Integrates with the OpenTelemetry activity sources configured in ServiceDefaults.
/// </summary>
internal sealed class TracingInterceptor : Interceptor
{
    private static readonly ActivitySource ActivitySource = new("DotNetCloud.Grpc.Server");

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        using var activity = ActivitySource.StartActivity(
            $"grpc {context.Method}",
            ActivityKind.Server);

        if (activity is not null)
        {
            activity.SetTag("rpc.system", "grpc");
            activity.SetTag("rpc.method", context.Method);
            activity.SetTag("rpc.service", ExtractServiceName(context.Method));

            if (context.UserState.TryGetValue("ModuleId", out var moduleId))
            {
                activity.SetTag("dotnetcloud.module_id", moduleId);
            }
        }

        try
        {
            var response = await continuation(request, context);

            activity?.SetTag("rpc.grpc.status_code", (int)StatusCode.OK);
            return response;
        }
        catch (RpcException ex)
        {
            activity?.SetTag("rpc.grpc.status_code", (int)ex.StatusCode);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Status.Detail);
            throw;
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var activity = ActivitySource.StartActivity(
            $"grpc {context.Method}",
            ActivityKind.Server);

        if (activity is not null)
        {
            activity.SetTag("rpc.system", "grpc");
            activity.SetTag("rpc.method", context.Method);
            activity.SetTag("rpc.service", ExtractServiceName(context.Method));
        }

        try
        {
            await continuation(request, responseStream, context);
            activity?.SetTag("rpc.grpc.status_code", (int)StatusCode.OK);
        }
        catch (RpcException ex)
        {
            activity?.SetTag("rpc.grpc.status_code", (int)ex.StatusCode);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Status.Detail);
            throw;
        }
    }

    private static string ExtractServiceName(string fullMethod)
    {
        // gRPC methods follow the format "/package.ServiceName/MethodName"
        var parts = fullMethod.Split('/');
        return parts.Length >= 2 ? parts[1] : fullMethod;
    }
}
