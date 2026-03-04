# Observability Architecture

> **Version:** 1.0  
> **Last Updated:** 2026-03-03  
> **Applies To:** DotNetCloud.Core.ServiceDefaults, DotNetCloud.Core.Server

---

## Overview

DotNetCloud provides a comprehensive observability stack built on industry-standard tools:

| Pillar | Technology | Purpose |
|--------|-----------|---------|
| **Logging** | Serilog | Structured logging with console, file, and module-level filtering |
| **Metrics** | OpenTelemetry + Prometheus | Runtime, HTTP, gRPC, and custom application metrics |
| **Tracing** | OpenTelemetry (W3C Trace Context) | Distributed tracing across modules and services |
| **Health Checks** | ASP.NET Core Health Checks | Liveness, readiness, and module health probes |

All observability components are configured via `AddDotNetCloudServiceDefaults()` and `UseDotNetCloudMiddleware()` extension methods in the `DotNetCloud.Core.ServiceDefaults` project.

---

## Logging (Serilog)

### Configuration

Serilog is configured through the `SerilogOptions` class, bindable from `appsettings.json`:

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
    "ExcludedModules": [],
    "ModuleLogLevels": {
      "DotNetCloud.Files": "Debug"
    }
  }
}
```

### Log Levels

| Level | Value | Usage |
|-------|-------|-------|
| Debug | 1 | Detailed internal state for development |
| Information | 2 | Normal operational events |
| Warning | 3 | Potentially harmful situations |
| Error | 4 | Failures that need attention |
| Fatal | 5 | Critical failures requiring immediate action |

### Sinks

- **Console** — Development: colored structured output. Production: plain structured output.
- **File** — Daily rolling files with configurable retention (default 31 days, 100 MB per file). Shared mode enabled for multi-process scenarios.

### Context Enrichment

Every log entry is automatically enriched with:

| Property | Source |
|----------|--------|
| `MachineName` | `Serilog.Enrichers.Environment` |
| `EnvironmentName` | `Serilog.Enrichers.Environment` |
| `ProcessId` | `Serilog.Enrichers.Process` |
| `ThreadId` | `Serilog.Enrichers.Thread` |

Additional context can be pushed per-request or per-operation via `LogEnricher`:

```csharp
using (LogEnricher.WithUserId(userId))
using (LogEnricher.WithRequestId(traceId))
using (LogEnricher.WithModuleName("dotnetcloud.files"))
{
    logger.LogInformation("File uploaded: {FileName}", fileName);
}
```

### Module-Level Filtering

The `ModuleLogFilter` allows per-module log level overrides and module exclusion. This is useful for noisy modules during development:

```json
{
  "Serilog": {
    "ExcludedModules": ["DotNetCloud.Debug"],
    "ModuleLogLevels": {
      "DotNetCloud.Files": "Debug",
      "DotNetCloud.Chat": "Warning"
    }
  }
}
```

### Request/Response Logging

The `RequestResponseLoggingMiddleware` (development only) logs:
- Incoming HTTP method, path, and remote IP
- Response status code and elapsed time
- Sensitive headers are automatically redacted (Authorization, Cookie, API keys)
- Health check and metrics endpoints are excluded from logging

---

## Metrics (OpenTelemetry)

### Built-in Instrumentation

The following metrics are collected automatically:

| Source | Meters |
|--------|--------|
| ASP.NET Core | `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.Server.Kestrel`, `Microsoft.AspNetCore.Routing` |
| HTTP Client | `System.Net.Http`, `System.Net.NameResolution` |
| .NET Runtime | GC, thread pool, JIT compilation |
| gRPC | `OpenTelemetry.Instrumentation.GrpcNetClient` |
| SignalR | `Microsoft.AspNetCore.Http.Connections` |

### Custom Meters

Modules can register additional meters via configuration:

```json
{
  "Telemetry": {
    "AdditionalMeters": ["DotNetCloud.Files.Operations"]
  }
}
```

### Exporters

| Exporter | Configuration | Use Case |
|----------|--------------|----------|
| **Console** | `EnableConsoleExporter: true` | Development debugging |
| **OTLP** | `OtlpEndpoint: "http://collector:4317"` | Production (Jaeger, Grafana Tempo, etc.) |
| **Prometheus** | `EnablePrometheusExporter: true` | Prometheus scraping at `/metrics` |

### Prometheus Integration

When `EnablePrometheusExporter` is `true`, the `/metrics` endpoint exposes all collected metrics in Prometheus exposition format. Configure Prometheus to scrape:

```yaml
scrape_configs:
  - job_name: 'dotnetcloud'
    scrape_interval: 15s
    static_configs:
      - targets: ['localhost:5080']
```

---

## Distributed Tracing (OpenTelemetry)

### Trace Propagation

DotNetCloud uses **W3C Trace Context** for distributed trace propagation across:
- HTTP requests (ASP.NET Core + HttpClient)
- gRPC calls (interceptor-based)
- SignalR connections

### Activity Sources

| Source | Purpose |
|--------|---------|
| `DotNetCloud.Core` | Core platform operations |
| `DotNetCloud.Modules` | Module lifecycle and cross-module calls |
| `DotNetCloud.Authentication` | Login, token issuance, MFA |
| `DotNetCloud.Authorization` | Permission checks, capability validation |

### Sampling

| Environment | Strategy | Rate |
|-------------|----------|------|
| Development | `AlwaysOnSampler` | 100% |
| Production | `ParentBasedSampler(TraceIdRatio)` | 10% (configurable) |

### Filtering

Health check endpoints (`/health/*`) are automatically excluded from tracing to reduce noise.

---

## Health Checks

### Endpoints

| Endpoint | Purpose | Checks Included |
|----------|---------|----------------|
| `GET /health` | Full health report | All registered checks |
| `GET /health/live` | Liveness probe | Self-diagnostic only (app is running) |
| `GET /health/ready` | Readiness probe | Startup, database, and module checks |

### Tag-Based Filtering

Health checks are registered with tags that control which endpoints include them:

| Tag | Included In |
|-----|-------------|
| `live` | `/health/live` |
| `ready` | `/health/ready` |
| `database` | `/health`, `/health/ready` |
| `module` | `/health`, `/health/ready` |

### Response Format

All health endpoints return structured JSON:

```json
{
  "status": "Healthy",
  "totalDuration": 42.5,
  "entries": {
    "self": {
      "status": "Healthy",
      "description": "Application is running.",
      "duration": 0.1,
      "exception": null,
      "data": null
    },
    "startup": {
      "status": "Healthy",
      "description": "Application has completed startup.",
      "duration": 0.2,
      "exception": null,
      "data": null
    }
  }
}
```

### Kubernetes Integration

For Kubernetes deployments, configure probes:

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 5080
  initialDelaySeconds: 5
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health/ready
    port: 5080
  initialDelaySeconds: 10
  periodSeconds: 15
```

### Custom Module Health Checks

Modules implement `IModuleHealthCheck` and register via `AddModuleHealthCheck()`:

```csharp
public class FilesModuleHealthCheck : IModuleHealthCheck
{
    public string ModuleName => "dotnetcloud.files";

    public async Task<ModuleHealthCheckResult> CheckHealthAsync(
        CancellationToken cancellationToken)
    {
        // Check storage connectivity, disk space, etc.
        return ModuleHealthCheckResult.Healthy("Storage accessible");
    }
}
```

### StartupHealthCheck

The `StartupHealthCheck` reports `Unhealthy` until `MarkReady()` is called during application startup. This prevents the readiness probe from routing traffic before initialization (database migration, module loading) is complete.

---

## Configuration Reference

### appsettings.json (Production)

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
    "ExcludedModules": [],
    "ModuleLogLevels": {}
  },
  "Telemetry": {
    "ServiceName": "DotNetCloud",
    "ServiceVersion": "1.0.0",
    "EnableMetrics": true,
    "EnableTracing": true,
    "EnableConsoleExporter": false,
    "EnablePrometheusExporter": false,
    "OtlpEndpoint": "",
    "AdditionalSources": [],
    "AdditionalMeters": []
  }
}
```

### appsettings.Development.json

```json
{
  "Serilog": {
    "ConsoleMinimumLevel": "Debug",
    "FileMinimumLevel": "Information",
    "FilePath": "logs/dotnetcloud-dev-.log",
    "RetainedFileCountLimit": 7,
    "UseStructuredFormat": true
  },
  "Telemetry": {
    "EnableMetrics": true,
    "EnableTracing": true,
    "EnableConsoleExporter": false,
    "EnablePrometheusExporter": false
  }
}
```

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────┐
│                  DotNetCloud Server                  │
│                                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────┐ │
│  │   Serilog     │  │ OpenTelemetry│  │  Health   │ │
│  │              │  │              │  │  Checks   │ │
│  │ Console Sink │  │  Metrics     │  │           │ │
│  │ File Sink    │  │  Tracing     │  │ /health   │ │
│  │ Log Filter   │  │              │  │ /health/  │ │
│  │ Enrichers    │  │              │  │   live    │ │
│  └──────┬───────┘  └──────┬───────┘  │ /health/  │ │
│         │                 │          │   ready   │ │
│         ▼                 ▼          └─────┬─────┘ │
│   Log Files          Exporters             │       │
│                   ┌────┴────┐              │       │
│                   │         │              │       │
│              Console    OTLP          Kubernetes   │
│              Exporter   Exporter      Probes       │
│                         Prometheus                 │
│                         (/metrics)                 │
└─────────────────────────────────────────────────────┘
```

---

## Files

| File | Purpose |
|------|---------|
| `ServiceDefaults/Logging/SerilogConfiguration.cs` | Serilog setup and options |
| `ServiceDefaults/Logging/LogEnricher.cs` | Log context enrichment |
| `ServiceDefaults/Logging/ModuleLogFilter.cs` | Per-module log filtering |
| `ServiceDefaults/Telemetry/TelemetryConfiguration.cs` | OpenTelemetry metrics, tracing, Prometheus |
| `ServiceDefaults/HealthChecks/StartupHealthCheck.cs` | Readiness startup probe |
| `ServiceDefaults/HealthChecks/DatabaseHealthCheck.cs` | Database connectivity check |
| `ServiceDefaults/HealthChecks/IModuleHealthCheck.cs` | Module health check interface |
| `ServiceDefaults/HealthChecks/ModuleHealthCheckAdapter.cs` | ASP.NET Core adapter |
| `ServiceDefaults/Middleware/RequestResponseLoggingMiddleware.cs` | HTTP logging with PII masking |
| `ServiceDefaults/Middleware/GlobalExceptionHandlerMiddleware.cs` | Exception-to-HTTP mapping |
| `ServiceDefaults/Extensions/ServiceDefaultsExtensions.cs` | One-line registration |
