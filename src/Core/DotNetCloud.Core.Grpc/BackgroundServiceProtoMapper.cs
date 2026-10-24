using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Services;
using Google.Protobuf.WellKnownTypes;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Maps core background service status snapshots to gRPC lifecycle messages so every
/// module host can expose them with a one-liner.
/// </summary>
public static class BackgroundServiceProtoMapper
{
    /// <summary>
    /// Maps a tracked service status to its gRPC representation.
    /// </summary>
    /// <param name="status">The tracked status.</param>
    /// <returns>The proto message.</returns>
    public static BackgroundServiceInfo ToProto(this BackgroundServiceStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new BackgroundServiceInfo
        {
            ServiceName = status.ServiceName,
            IsRunning = status.IsRunning,
            CurrentRunStartedAt = status.CurrentRunStartedAt?.ToTimestamp(),
            LastRunAt = status.LastRunAt.ToTimestamp(),
            LastRunDurationMs = status.LastRunDuration.TotalMilliseconds,
            LastRunSuccess = status.LastRunSuccess,
            LastRunMessage = status.LastRunMessage ?? string.Empty,
            TotalRuns = status.TotalRuns,
            TotalFailures = status.TotalFailures,
        };
    }

    /// <summary>
    /// Builds a <see cref="GetBackgroundServicesResponse"/> from a tracker.
    /// </summary>
    /// <param name="tracker">The background service tracker.</param>
    /// <returns>A response containing every tracked service.</returns>
    public static GetBackgroundServicesResponse ToResponse(this IBackgroundServiceTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);

        var response = new GetBackgroundServicesResponse();
        response.Services.AddRange(tracker.GetAll().Values.Select(s => s.ToProto()));
        return response;
    }
}
