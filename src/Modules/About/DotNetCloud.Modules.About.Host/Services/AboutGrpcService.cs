using DotNetCloud.Modules.About.Host.Protos;
using Grpc.Core;

namespace DotNetCloud.Modules.About.Host.Services;

/// <summary>
/// gRPC service for the About module — provides system information, version details,
/// and license status without requiring a database.
/// </summary>
public sealed class AboutGrpcService : AboutService.AboutServiceBase
{
    private readonly AboutModule _module;
    private readonly ILogger<AboutGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutGrpcService"/> class.
    /// </summary>
    public AboutGrpcService(AboutModule module, ILogger<AboutGrpcService> logger)
    {
        _module = module;
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task<AboutInfoResponse> GetAboutInfo(
        GetAboutInfoRequest request, ServerCallContext context)
    {
        try
        {
            return Task.FromResult(new AboutInfoResponse
            {
                Success = true,
                Version = _module.Manifest.Version,
                Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                RuntimeVersion = System.Environment.Version.ToString(),
                OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                LicenseStatus = "MIT",
                Uptime = System.Environment.TickCount64.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAboutInfo failed");
            return Task.FromResult(new AboutInfoResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}
