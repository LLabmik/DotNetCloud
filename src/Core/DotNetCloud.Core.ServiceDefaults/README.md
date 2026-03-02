# DotNetCloud.Core.ServiceDefaults

This library provides shared infrastructure and cross-cutting concerns for all DotNetCloud services and modules.

## Features

### 🔍 Logging (Serilog)

- **Structured Logging**: Consistent JSON-formatted logs
- **Multiple Sinks**: Console (development) and File (production)
- **Log Enrichment**: Automatic context enrichment with user ID, request ID, module name
- **Module-Specific Filtering**: Configure log levels per module
- **Sensitive Data Masking**: Automatic redaction of sensitive information

### 📊 Telemetry (OpenTelemetry)

- **Metrics Collection**: HTTP, gRPC, database, and runtime metrics
- **Distributed Tracing**: W3C Trace Context propagation
- **Activity Sources**: Pre-configured sources for core operations
- **OTLP Export**: Send telemetry data to collectors (Prometheus, Jaeger, etc.)
- **Development Mode**: Console exporter for local debugging

### ❤️ Health Checks

- **Database Health Check**: Multi-provider support (PostgreSQL, SQL Server, MariaDB)
- **Module Health Checks**: Interface for module-specific health monitoring
- **Multiple Endpoints**: `/health`, `/health/ready`, `/health/live`

### 🔒 Security Middleware

- **Content-Security-Policy**: Prevent XSS and injection attacks
- **X-Frame-Options**: Clickjacking protection
- **X-Content-Type-Options**: MIME-sniffing protection
- **Strict-Transport-Security**: Enforce HTTPS
- **Referrer-Policy**: Control referrer information
- **Permissions-Policy**: Control browser features
- **Server Header Removal**: Hide server information

### 🛡️ Error Handling

- **Global Exception Handler**: Consistent error responses
- **Status Code Mapping**: Automatic HTTP status code assignment
- **Request Tracking**: Error correlation with request IDs
- **Stack Trace Control**: Include/exclude stack traces based on environment

### 📝 Request/Response Logging

- **Automatic Request Logging**: Method, path, headers
- **Automatic Response Logging**: Status code, elapsed time
- **Sensitive Data Masking**: Authorization headers, cookies, API keys
- **Excluded Paths**: Skip logging for health checks and metrics

## Installation

```bash
dotnet add package DotNetCloud.Core.ServiceDefaults
```

## Usage

### Basic Setup (Web Application)

```csharp
using DotNetCloud.Core.ServiceDefaults.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (logging, telemetry, health checks)
builder.AddDotNetCloudServiceDefaults();

// Add your services
builder.Services.AddControllers();

var app = builder.Build();

// Use middleware defaults (security headers, exception handling, logging)
app.UseDotNetCloudMiddleware();

// Map health check endpoints
app.MapDotNetCloudHealthChecks();

// Map your endpoints
app.MapControllers();

app.Run();
```

### Custom Configuration

```csharp
builder.AddDotNetCloudServiceDefaults(
    configureSerilog: serilog =>
    {
        serilog.ConsoleMinimumLevel = LogEventLevel.Debug;
        serilog.FilePath = "logs/myapp-.log";
        serilog.RetainedFileCountLimit = 7;
        serilog.ModuleLogLevels["MyModule"] = LogEventLevel.Trace;
    },
    configureTelemetry: telemetry =>
    {
        telemetry.ServiceName = "MyService";
        telemetry.ServiceVersion = "2.0.0";
        telemetry.EnableConsoleExporter = true;
        telemetry.OtlpEndpoint = "http://localhost:4317";
        telemetry.AdditionalSources.Add("MyCustomSource");
    });
```

### Configuration (appsettings.json)

```json
{
  "Serilog": {
    "ConsoleMinimumLevel": "Information",
    "FileMinimumLevel": "Warning",
    "FilePath": "logs/dotnetcloud-.log",
    "RollingDaily": true,
    "RetainedFileCountLimit": 31,
    "FileSizeLimitBytes": 104857600,
    "UseStructuredFormat": true,
    "ModuleLogLevels": {
      "DotNetCloud.Modules.Files": "Debug",
      "DotNetCloud.Modules.Chat": "Information"
    }
  },
  "Telemetry": {
    "ServiceName": "DotNetCloud",
    "ServiceVersion": "1.0.0",
    "EnableMetrics": true,
    "EnableTracing": true,
    "OtlpEndpoint": "http://localhost:4317",
    "EnableConsoleExporter": false
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5000",
      "https://dotnetcloud.example.com"
    ]
  }
}
```

### Log Enrichment

```csharp
using DotNetCloud.Core.ServiceDefaults.Logging;

// Enrich with user ID
using (LogEnricher.WithUserId(userId))
{
    _logger.LogInformation("User action performed");
}

// Enrich with request ID
using (LogEnricher.WithRequestId(requestId))
{
    _logger.LogInformation("Processing request");
}

// Enrich with module name
using (LogEnricher.WithModuleName("Files"))
{
    _logger.LogInformation("File operation completed");
}

// Enrich with caller context
using (LogEnricher.WithCallerContext(callerContext))
{
    _logger.LogInformation("Authorized operation");
}
```

### Custom Health Checks

```csharp
using DotNetCloud.Core.ServiceDefaults.HealthChecks;

public class MyModuleHealthCheck : IModuleHealthCheck
{
    public string ModuleName => "MyModule";

    public async Task<ModuleHealthCheckResult> CheckHealthAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform health check logic
            var isHealthy = await CheckModuleStatusAsync(cancellationToken);
            
            return isHealthy
                ? ModuleHealthCheckResult.Healthy("Module is operational")
                : ModuleHealthCheckResult.Degraded("Module is degraded");
        }
        catch (Exception ex)
        {
            return ModuleHealthCheckResult.Unhealthy("Module is unhealthy", ex);
        }
    }
}

// Register the health check
builder.Services.AddModuleHealthCheck(new MyModuleHealthCheck());
```

### Custom Activity Sources

```csharp
using DotNetCloud.Core.ServiceDefaults.Telemetry;
using System.Diagnostics;

public class MyService
{
    private static readonly ActivitySource _activitySource = 
        new ActivitySource("MyCompany.MyService");

    public async Task DoWorkAsync()
    {
        using var activity = _activitySource.StartActivity("DoWork");
        activity?.SetTag("workType", "important");

        // Do work
        await Task.Delay(100);

        activity?.SetTag("result", "success");
    }
}

// Register the activity source in telemetry configuration
builder.AddDotNetCloudServiceDefaults(
    configureTelemetry: telemetry =>
    {
        telemetry.AdditionalSources.Add("MyCompany.MyService");
    });
```

### Security Headers Configuration

```csharp
app.UseDotNetCloudMiddleware(configureSecurityHeaders: headers =>
{
    headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data: https:";
    headers.XFrameOptionsValue = "SAMEORIGIN";
    headers.StrictTransportSecurityMaxAge = 63072000; // 2 years
});
```

## Architecture

### Logging Flow

```
Application Code
    ↓
Serilog Logger
    ↓
Log Enrichers (UserId, RequestId, ModuleName)
    ↓
Module Log Filter
    ↓
Sinks (Console, File)
```

### Telemetry Flow

```
Application Code
    ↓
Activity Sources / Meters
    ↓
OpenTelemetry SDK
    ↓
Instrumentation (ASP.NET Core, HttpClient, gRPC)
    ↓
Exporters (Console, OTLP)
    ↓
Collectors (Prometheus, Jaeger, etc.)
```

### Middleware Pipeline

```
Request
    ↓
SecurityHeadersMiddleware
    ↓
GlobalExceptionHandlerMiddleware
    ↓
RequestResponseLoggingMiddleware
    ↓
Application Middleware
    ↓
Response
```

## Best Practices

### Logging

- Use structured logging with named parameters: `_logger.LogInformation("User {UserId} logged in", userId)`
- Enrich logs with context using `LogEnricher`
- Configure module-specific log levels to reduce noise
- Use appropriate log levels (Trace, Debug, Information, Warning, Error, Fatal)

### Telemetry

- Create custom activity sources for business operations
- Add tags/attributes to activities for filtering
- Use consistent naming for metrics and traces
- Export to OTLP collector for production

### Health Checks

- Implement `IModuleHealthCheck` for each module
- Return appropriate status (Healthy, Degraded, Unhealthy)
- Include diagnostic data in health check results
- Monitor health check endpoints in production

### Security

- Review and customize security headers for your use case
- Always use HTTPS in production
- Configure CORS carefully (don't use `AllowAnyOrigin` in production)
- Mask sensitive data in logs and error responses

## Dependencies

- **Serilog** (4.3.0): Structured logging
- **OpenTelemetry** (1.10.0): Metrics and tracing
- **ASP.NET Core Health Checks**: Health monitoring
- **Microsoft.Extensions.Hosting**: Hosting abstractions

## License

This project is licensed under the AGPL-3.0 License.

## Contributing

See [CONTRIBUTING.md](../../../CONTRIBUTING.md) for contribution guidelines.
