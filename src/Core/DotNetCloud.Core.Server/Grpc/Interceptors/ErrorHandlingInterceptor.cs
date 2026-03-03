using DotNetCloud.Core.Errors;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// gRPC interceptor that catches unhandled exceptions and converts them
/// to appropriate gRPC status codes with structured error details.
/// </summary>
internal sealed class ErrorHandlingInterceptor : Interceptor
{
    private readonly ILogger<ErrorHandlingInterceptor> _logger;
    private readonly IHostEnvironment _environment;

    public ErrorHandlingInterceptor(ILogger<ErrorHandlingInterceptor> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            // Already a gRPC exception — pass through
            throw;
        }
        catch (Exception ex)
        {
            throw ConvertToRpcException(ex, context);
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ConvertToRpcException(ex, context);
        }
    }

    private RpcException ConvertToRpcException(Exception ex, ServerCallContext context)
    {
        var (statusCode, errorCode) = ex switch
        {
            CapabilityNotGrantedException => (StatusCode.PermissionDenied, ErrorCodes.CapabilityNotGranted),
            Errors.UnauthorizedException => (StatusCode.Unauthenticated, ErrorCodes.Unauthorized),
            Errors.ForbiddenException => (StatusCode.PermissionDenied, ErrorCodes.Forbidden),
            Errors.NotFoundException => (StatusCode.NotFound, ErrorCodes.EntityNotFound),
            Errors.ValidationException => (StatusCode.InvalidArgument, ErrorCodes.ValidationError),
            Errors.ConcurrencyException => (StatusCode.Aborted, ErrorCodes.ConcurrencyConflict),
            ModuleNotFoundException => (StatusCode.NotFound, ErrorCodes.ModuleNotFound),
            ArgumentException => (StatusCode.InvalidArgument, ErrorCodes.ValidationError),
            OperationCanceledException => (StatusCode.Cancelled, "CANCELLED"),
            NotImplementedException => (StatusCode.Unimplemented, "NOT_IMPLEMENTED"),
            _ => (StatusCode.Internal, ErrorCodes.InternalServerError)
        };

        var message = _environment.IsDevelopment()
            ? $"[{errorCode}] {ex.Message}"
            : $"[{errorCode}] An error occurred processing the request.";

        _logger.LogError(ex, "gRPC error on {Method}: {ErrorCode}", context.Method, errorCode);

        var metadata = new Metadata { { "error-code", errorCode } };

        return new RpcException(new Status(statusCode, message), metadata);
    }
}
