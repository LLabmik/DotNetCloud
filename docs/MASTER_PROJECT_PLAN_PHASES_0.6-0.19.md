# MASTER_PROJECT_PLAN - Phases 0.6 through 0.19

> **Status:** Draft - To be integrated into main MASTER_PROJECT_PLAN.md  
> **Purpose:** Detailed step breakdowns for remaining Phase 0 sections  
> **Created:** 2026-03-02

---

## Section: Phase 0.6 - Process Supervisor & gRPC Host

### Step: phase-0.6.1 - Process Supervisor Core
**Status:** pending  
**Duration:** ~3 hours  
**Description:** Create module process spawning and monitoring

**Recommended Prompt:**
```
Execute phase-0.6.1: Create process supervisor. Implement ProcessSupervisor service that spawns module 
processes, tracks process IDs, monitors process health. Use System.Diagnostics.Process for spawning. 
Configure working directories per module. Handle process stdout/stderr logging.
Location: src/Core/DotNetCloud.Core.Server/Supervisor/
```

**Deliverables:**
- ☐ `ProcessSupervisor` service
- ☐ Module process spawning logic
- ☐ Process ID tracking
- ☐ Process output logging (stdout/stderr)
- ☐ Working directory per module

**File Location:** `src/Core/DotNetCloud.Core.Server/Supervisor/`  
**Dependencies:** phase-0.5.4 (module loading)  
**Testing:** Process lifecycle tests  
**Notes:** Foundation for module isolation

---

### Step: phase-0.6.2 - gRPC Health Checks
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Implement periodic gRPC health checks for modules

**Recommended Prompt:**
```
Execute phase-0.6.2: Create gRPC health monitoring. Implement periodic health checks using gRPC 
Health Checking Protocol. Check module health every 30 seconds (configurable). Track health status 
(Healthy, Degraded, Unhealthy). Log health status changes. Trigger restart policies on failures.
Location: src/Core/DotNetCloud.Core.Server/Supervisor/
```

**Deliverables:**
- ☐ Periodic health check implementation
- ☐ gRPC Health Checking Protocol client
- ☐ Health status tracking (Healthy/Degraded/Unhealthy)
- ☐ Configurable check intervals
- ☐ Health status change logging

**File Location:** `src/Core/DotNetCloud.Core.Server/Supervisor/`  
**Dependencies:** phase-0.6.1  
**Testing:** Health check tests  
**Notes:** Enables proactive failure detection

---

### Step: phase-0.6.3 - Restart Policies
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Implement configurable restart policies for failed modules

**Recommended Prompt:**
```
Execute phase-0.6.3: Create restart policies. Implement three restart strategies: Immediate 
(restart immediately), Exponential Backoff (1s, 2s, 4s, 8s, max 60s), Alert-Only (no auto-restart, 
alert admin). Configure per module in manifest. Track restart attempts. Stop retrying after max 
attempts (default 5).
Location: src/Core/DotNetCloud.Core.Server/Supervisor/
```

**Deliverables:**
- ☐ Immediate restart strategy
- ☐ Exponential backoff strategy
- ☐ Alert-only strategy
- ☐ Per-module configuration
- ☐ Restart attempt tracking

**File Location:** `src/Core/DotNetCloud.Core.Server/Supervisor/`  
**Dependencies:** phase-0.6.2  
**Testing:** Restart policy tests  
**Notes:** Balances availability and stability

---

### Step: phase-0.6.4 - Graceful Shutdown
**Status:** pending  
**Duration:** ~2.5 hours  
**Description:** Implement graceful module termination

**Recommended Prompt:**
```
Execute phase-0.6.4: Create graceful shutdown. Signal module to stop (SIGTERM on Linux, 
WM_CLOSE on Windows), wait for graceful termination (30s default timeout), force kill if timeout 
exceeded (SIGKILL/TerminateProcess). Drain active connections before shutdown. Log shutdown events.
Location: src/Core/DotNetCloud.Core.Server/Supervisor/
```

**Deliverables:**
- ☐ Graceful termination signal (SIGTERM/WM_CLOSE)
- ☐ Configurable grace period (30s default)
- ☐ Force kill after timeout
- ☐ Connection draining
- ☐ Shutdown event logging

**File Location:** `src/Core/DotNetCloud.Core.Server/Supervisor/`  
**Dependencies:** phase-0.6.3  
**Testing:** Shutdown scenario tests  
**Notes:** Prevents data loss on shutdown

---

### Step: phase-0.6.5 - Resource Limits (Linux cgroups)
**Status:** pending  
**Duration:** ~2.5 hours  
**Description:** Implement CPU and memory limits using Linux cgroups

**Recommended Prompt:**
```
Execute phase-0.6.5: Create resource limits for Linux. Use cgroups v2 to limit CPU (quota/period) 
and memory (max bytes) per module process. Configure limits in module manifest. Monitor resource usage. 
Kill processes exceeding limits. Log resource violations.
Location: src/Core/DotNetCloud.Core.Server/Supervisor/
```

**Deliverables:**
- ☐ cgroups v2 integration
- ☐ CPU quota/period configuration
- ☐ Memory limit configuration
- ☐ Resource usage monitoring
- ☐ Limit enforcement (kill on exceed)

**File Location:** `src/Core/DotNetCloud.Core.Server/Supervisor/`  
**Dependencies:** phase-0.6.4  
**Testing:** Resource limit tests (Linux only)  
**Notes:** Linux-specific feature

---

### Step: phase-0.6.6 - Resource Limits (Windows Job Objects)
**Status:** pending  
**Duration:** ~2.5 hours  
**Description:** Implement CPU and memory limits using Windows Job Objects

**Recommended Prompt:**
```
Execute phase-0.6.6: Create resource limits for Windows. Use Windows Job Objects to limit CPU 
(affinity, priority) and memory (max working set) per module process. Configure limits in module 
manifest. Monitor resource usage. Terminate processes exceeding limits. Log resource violations.
Location: src/Core/DotNetCloud.Core.Server/Supervisor/
```

**Deliverables:**
- ☐ Windows Job Objects integration
- ☐ CPU affinity/priority configuration
- ☐ Memory working set limit
- ☐ Resource usage monitoring
- ☐ Limit enforcement (terminate on exceed)

**File Location:** `src/Core/DotNetCloud.Core.Server/Supervisor/`  
**Dependencies:** phase-0.6.5  
**Testing:** Resource limit tests (Windows only)  
**Notes:** Windows-specific feature

---

### Step: phase-0.6.7 - gRPC Server Configuration
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Configure gRPC server with Unix domain sockets and named pipes

**Recommended Prompt:**
```
Execute phase-0.6.7: Configure gRPC server. Set up Unix domain socket listener on Linux 
(/var/run/dotnetcloud/modules/{moduleId}.sock), named pipe listener on Windows 
(\\.\pipe\dotnetcloud\{moduleId}), TCP fallback for Docker/Kubernetes (random port). 
Configure TLS for TCP. Create socket/pipe cleanup on shutdown.
Location: src/Core/DotNetCloud.Core.Server/Grpc/
```

**Deliverables:**
- ☐ Unix domain socket support (Linux)
- ☐ Named pipe support (Windows)
- ☐ TCP fallback (Docker/Kubernetes)
- ☐ TLS configuration for TCP
- ☐ Socket/pipe cleanup

**File Location:** `src/Core/DotNetCloud.Core.Server/Grpc/`  
**Dependencies:** phase-0.6.6  
**Testing:** Transport tests per platform  
**Notes:** Enables secure IPC

---

### Step: phase-0.6.8 - gRPC Health Service
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Implement gRPC Health Checking Protocol service

**Recommended Prompt:**
```
Execute phase-0.6.8: Create gRPC health service. Implement grpc.health.v1.Health service per spec. 
Return health status (SERVING, NOT_SERVING, UNKNOWN) for module services. Support Watch RPC for 
streaming health updates. Register health service in gRPC server.
Location: src/Core/DotNetCloud.Core.Server/Grpc/
```

**Deliverables:**
- ☐ grpc.health.v1.Health service implementation
- ☐ Health status reporting (SERVING/NOT_SERVING/UNKNOWN)
- ☐ Watch RPC for streaming updates
- ☐ Service registration

**File Location:** `src/Core/DotNetCloud.Core.Server/Grpc/`  
**Dependencies:** phase-0.6.7  
**Testing:** Health service tests  
**Notes:** Standard gRPC health protocol

---

### Step: phase-0.6.9 - gRPC Interceptors (Authentication)
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Create gRPC interceptor for authentication and authorization

**Recommended Prompt:**
```
Execute phase-0.6.9: Create auth interceptor. Implement gRPC server interceptor that validates 
bearer tokens from metadata (authorization: Bearer <token>), extracts CallerContext from token 
claims, injects CallerContext into request context. Reject unauthenticated calls with UNAUTHENTICATED 
status. Handle authorization failures with PERMISSION_DENIED.
Location: src/Core/DotNetCloud.Core.Server/Grpc/
```

**Deliverables:**
- ☐ Authentication interceptor
- ☐ Token validation from metadata
- ☐ CallerContext extraction
- ☐ Request context injection
- ☐ Error handling (UNAUTHENTICATED/PERMISSION_DENIED)

**File Location:** `src/Core/DotNetCloud.Core.Server/Grpc/`  
**Dependencies:** phase-0.4.7 (token validation)  
**Testing:** Auth interceptor tests  
**Notes:** Secures gRPC endpoints

---

### Step: phase-0.6.10 - gRPC Interceptors (Tracing & Logging)
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create gRPC interceptors for distributed tracing and logging

**Recommended Prompt:**
```
Execute phase-0.6.10: Create observability interceptors. Implement distributed tracing interceptor 
that propagates W3C Trace Context, creates spans per gRPC call, records call duration. Implement 
logging interceptor that logs request/response metadata, call duration, errors. Integrate with 
OpenTelemetry.
Location: src/Core/DotNetCloud.Core.Server/Grpc/
```

**Deliverables:**
- ☐ Distributed tracing interceptor
- ☐ W3C Trace Context propagation
- ☐ Span creation per call
- ☐ Logging interceptor
- ☐ OpenTelemetry integration

**File Location:** `src/Core/DotNetCloud.Core.Server/Grpc/`  
**Dependencies:** phase-0.3.3 (OpenTelemetry)  
**Testing:** Tracing/logging tests  
**Notes:** Enables observability

---

### Step: phase-0.6.11 - gRPC Error Handling Interceptor
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Create gRPC interceptor for consistent error handling

**Recommended Prompt:**
```
Execute phase-0.6.11: Create error handling interceptor. Catch exceptions in gRPC calls, 
map to appropriate gRPC status codes (INVALID_ARGUMENT, NOT_FOUND, INTERNAL, etc.), 
include error details in status message, log exceptions with context. Return structured errors.
Location: src/Core/DotNetCloud.Core.Server/Grpc/
```

**Deliverables:**
- ☐ Error handling interceptor
- ☐ Exception to status code mapping
- ☐ Error details in status message
- ☐ Exception logging

**File Location:** `src/Core/DotNetCloud.Core.Server/Grpc/`  
**Dependencies:** phase-0.6.10  
**Testing:** Error handling tests  
**Notes:** Consistent error responses

---

### Step: phase-0.6.12 - gRPC Service Contracts
**Status:** pending  
**Duration:** ~3 hours  
**Description:** Define .proto files for core capability interfaces

**Recommended Prompt:**
```
Execute phase-0.6.12: Create gRPC service definitions. Define .proto files for core capabilities: 
UserDirectory.proto, CurrentUserContext.proto, NotificationService.proto, StorageProvider.proto, 
ModuleSettings.proto, TeamDirectory.proto, UserManager.proto, BackupProvider.proto. 
Follow gRPC style guide. Generate C# code from protos.
Location: src/Core/DotNetCloud.Core.Server/Protos/
```

**Deliverables:**
- ☐ UserDirectory.proto
- ☐ CurrentUserContext.proto
- ☐ NotificationService.proto
- ☐ StorageProvider.proto
- ☐ ModuleSettings.proto
- ☐ TeamDirectory.proto
- ☐ UserManager.proto
- ☐ BackupProvider.proto

**File Location:** `src/Core/DotNetCloud.Core.Server/Protos/`  
**Dependencies:** phase-0.1.1 (capability interfaces)  
**Testing:** Proto compilation tests  
**Notes:** gRPC contract definitions

---

### Step: phase-0.6.13 - Process Supervisor Integration Tests
**Status:** pending  
**Duration:** ~2.5 hours  
**Description:** Create comprehensive supervisor tests

**Recommended Prompt:**
```
Execute phase-0.6.13: Create supervisor tests. Test process spawning, health checks, restart policies, 
graceful shutdown, resource limits (platform-specific). Create test modules that simulate failures. 
Test gRPC communication. Target 80%+ coverage.
Location: tests/DotNetCloud.Core.Server.Tests/Supervisor/
```

**Deliverables:**
- ☐ Process lifecycle tests
- ☐ Health check tests
- ☐ Restart policy tests
- ☐ Shutdown tests
- ☐ Resource limit tests

**File Location:** `tests/DotNetCloud.Core.Server.Tests/Supervisor/`  
**Dependencies:** phase-0.6.1 through phase-0.6.12  
**Testing:** 80%+ code coverage  
**Notes:** Validates supervisor reliability

---

## Section: Phase 0.7 - Web Server & API Foundation

### Step: phase-0.7.1 - Kestrel Server Configuration
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Configure Kestrel with HTTPS and HTTP/2

**Recommended Prompt:**
```
Execute phase-0.7.1: Configure Kestrel server. Set up HTTPS with TLS 1.2+ (require strong ciphers), 
configure HTTP/2 support, set listener addresses (http://localhost:5000, https://localhost:5001), 
configure request limits (max body size, header limits), enable compression (gzip, brotli).
Location: src/Core/DotNetCloud.Core.Server/Program.cs
```

**Deliverables:**
- ☐ Kestrel server configuration
- ☐ HTTPS/TLS setup (TLS 1.2+)
- ☐ HTTP/2 support
- ☐ Listener address configuration
- ☐ Request limits
- ☐ Response compression (gzip/brotli)

**File Location:** `src/Core/DotNetCloud.Core.Server/Program.cs`  
**Dependencies:** None  
**Testing:** Server startup tests  
**Notes:** Foundation for web hosting

---

### Step: phase-0.7.2 - Reverse Proxy Configuration Templates
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Generate IIS, Apache, nginx configuration templates

**Recommended Prompt:**
```
Execute phase-0.7.2: Create reverse proxy templates. Generate IIS web.config with ANCM configuration, 
Apache .htaccess with mod_proxy rules, nginx .conf with upstream configuration. Include WebSocket 
support, HTTPS redirect, security headers. Create template generator CLI command.
Location: tools/ReverseProxyGenerator/
```

**Deliverables:**
- ☐ IIS ANCM web.config template
- ☐ Apache mod_proxy template
- ☐ nginx configuration template
- ☐ WebSocket support in all templates
- ☐ Template generator CLI tool

**File Location:** `tools/ReverseProxyGenerator/`  
**Dependencies:** phase-0.7.1  
**Testing:** Template validation tests  
**Notes:** Deployment flexibility

---

### Step: phase-0.7.3 - API Versioning Setup
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Implement URL-based API versioning

**Recommended Prompt:**
```
Execute phase-0.7.3: Set up API versioning. Use Asp.Versioning.Mvc package. Configure URL-based 
versioning (/api/v1/, /api/v2/). Set default version to 1.0. Add version deprecation warnings via 
response headers (Sunset, Deprecation). Create versioning documentation.
Location: src/Core/DotNetCloud.Core.Server/
```

**Deliverables:**
- ☐ Asp.Versioning.Mvc package integration
- ☐ URL-based versioning (/api/v1/, /api/v2/)
- ☐ Default version configuration
- ☐ Deprecation warning headers
- ☐ Versioning documentation

**File Location:** `src/Core/DotNetCloud.Core.Server/`  
**Dependencies:** phase-0.7.1  
**Testing:** Versioning tests  
**Notes:** Enables API evolution

---

### Step: phase-0.7.4 - Response Envelope Middleware
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Create standard response envelope for all API responses

**Recommended Prompt:**
```
Execute phase-0.7.4: Create response envelope. Implement middleware that wraps all API responses in 
ApiSuccessResponse<T> with success=true, data=<payload>, pagination=<info>. Wrap errors in ApiErrorResponse 
with success=false, code=<error_code>, message=<message>, details=<details>. Exclude from Swagger/health 
endpoints.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/
```

**Deliverables:**
- ☐ Response envelope middleware
- ☐ ApiSuccessResponse<T> wrapping
- ☐ ApiErrorResponse wrapping
- ☐ Pagination support
- ☐ Endpoint exclusion configuration

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/`  
**Dependencies:** phase-0.1.6 (error models)  
**Testing:** Envelope format tests  
**Notes:** Consistent API responses

---

### Step: phase-0.7.5 - Rate Limiting Middleware
**Status:** pending  
**Duration:** ~2.5 hours  
**Description:** Implement token bucket rate limiting

**Recommended Prompt:**
```
Execute phase-0.7.5: Create rate limiting. Use System.Threading.RateLimiting. Implement token bucket 
algorithm (100 req/min per user, 1000 req/min per IP by default). Configure limits per endpoint. 
Add X-RateLimit-* headers (Limit, Remaining, Reset). Return 429 Too Many Requests on exceed. 
Create admin override capability.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/
```

**Deliverables:**
- ☐ Token bucket rate limiter
- ☐ Per-user and per-IP limits
- ☐ Per-endpoint configuration
- ☐ X-RateLimit-* response headers
- ☐ 429 status code on exceed
- ☐ Admin override capability

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/`  
**Dependencies:** phase-0.7.4  
**Testing:** Rate limit tests  
**Notes:** Prevents abuse

---

### Step: phase-0.7.6 - OpenAPI/Swagger Configuration
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Configure Swagger UI and OpenAPI schema generation

**Recommended Prompt:**
```
Execute phase-0.7.6: Set up Swagger. Install Swashbuckle.AspNetCore package. Configure OpenAPI 
document generation with API info, version, description. Add XML documentation comments to OpenAPI 
schema. Configure Bearer token authentication in Swagger UI. Enable Swagger UI at /swagger. 
Generate openapi.json at /swagger/v1/swagger.json.
Location: src/Core/DotNetCloud.Core.Server/
```

**Deliverables:**
- ☐ Swashbuckle.AspNetCore package
- ☐ OpenAPI document generation
- ☐ XML comment integration
- ☐ Bearer token auth in Swagger UI
- ☐ Swagger UI endpoint (/swagger)
- ☐ OpenAPI JSON endpoint

**File Location:** `src/Core/DotNetCloud.Core.Server/`  
**Dependencies:** phase-0.7.5  
**Testing:** OpenAPI schema validation  
**Notes:** API documentation

---

### Step: phase-0.7.7 - CORS Configuration
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Configure Cross-Origin Resource Sharing policies

**Recommended Prompt:**
```
Execute phase-0.7.7: Configure CORS. Create default CORS policy allowing specific origins 
(configurable in appsettings.json), all methods, specific headers (Authorization, Content-Type, 
X-Requested-With), credentials enabled. Add CORS middleware. Support wildcard origins for development.
Location: src/Core/DotNetCloud.Core.Server/
```

**Deliverables:**
- ☐ CORS policy configuration
- ☐ Origin whitelist (configurable)
- ☐ Methods/headers configuration
- ☐ Credentials support
- ☐ Development wildcard support

**File Location:** `src/Core/DotNetCloud.Core.Server/`  
**Dependencies:** phase-0.7.6  
**Testing:** CORS tests  
**Notes:** Enables web client access

---

### Step: phase-0.7.8 - Controller Base Classes
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create base controller classes with common functionality

**Recommended Prompt:**
```
Execute phase-0.7.8: Create controller base classes. Implement ApiControllerBase with common methods: 
GetCurrentUser(), GetCallerContext(), Ok<T>(data), Created<T>(data, location), BadRequest(errors), 
NotFound(message), Unauthorized(), Forbidden(). Add [ApiController], [Authorize], [Route] attributes.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ `ApiControllerBase` class
- ☐ GetCurrentUser() helper
- ☐ GetCallerContext() helper
- ☐ Standard response methods
- ☐ Common attributes

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.8 (Identity)  
**Testing:** Base controller tests  
**Notes:** Reduces controller boilerplate

---

### Step: phase-0.7.9 - Model Validation
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Configure automatic model validation and error responses

**Recommended Prompt:**
```
Execute phase-0.7.9: Configure model validation. Enable automatic validation via [ApiController] attribute. 
Customize validation error responses (return 400 with field-specific errors). Use FluentValidation for 
complex validation rules. Create custom validators for DTOs. Return structured validation errors.
Location: src/Core/DotNetCloud.Core.Server/Validation/
```

**Deliverables:**
- ☐ Automatic model validation
- ☐ Custom validation error responses
- ☐ FluentValidation integration
- ☐ Custom validators for DTOs
- ☐ Structured error format

**File Location:** `src/Core/DotNetCloud.Core.Server/Validation/`  
**Dependencies:** phase-0.7.8  
**Testing:** Validation tests  
**Notes:** Input validation

---

### Step: phase-0.7.10 - Pagination Support
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create pagination helpers and response models

**Recommended Prompt:**
```
Execute phase-0.7.10: Create pagination support. Implement PaginatedList<T> model with items, totalCount, 
pageNumber, pageSize, totalPages. Create ToPagedListAsync() extension for IQueryable<T>. Add pagination 
parameters to controller methods (page=1, pageSize=20 defaults). Include pagination info in response envelope.
Location: src/Core/DotNetCloud.Core/Models/
```

**Deliverables:**
- ☐ `PaginatedList<T>` model
- ☐ `ToPagedListAsync()` extension
- ☐ Pagination query parameters
- ☐ Response envelope integration

**File Location:** `src/Core/DotNetCloud.Core/Models/`  
**Dependencies:** phase-0.7.4 (response envelope)  
**Testing:** Pagination tests  
**Notes:** Efficient large dataset handling

---

### Step: phase-0.7.11 - Filtering & Sorting Support
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Create dynamic filtering and sorting for list endpoints

**Recommended Prompt:**
```
Execute phase-0.7.11: Create filtering/sorting support. Implement ApplyFilters() and ApplySort() 
extensions for IQueryable<T>. Support query parameters: filter[field]=value, sort=field:asc,field2:desc. 
Handle type conversions, null checks, invalid field names. Prevent SQL injection via parameterized queries.
Location: src/Core/DotNetCloud.Core/Extensions/
```

**Deliverables:**
- ☐ `ApplyFilters()` extension
- ☐ `ApplySort()` extension
- ☐ Query parameter parsing
- ☐ Type conversion handling
- ☐ SQL injection prevention

**File Location:** `src/Core/DotNetCloud.Core/Extensions/`  
**Dependencies:** phase-0.7.10  
**Testing:** Filtering/sorting tests  
**Notes:** Flexible data querying

---

### Step: phase-0.7.12 - Content Negotiation
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Configure JSON and XML content negotiation

**Recommended Prompt:**
```
Execute phase-0.7.12: Configure content negotiation. Support JSON (default), XML (optional). 
Use System.Text.Json with camelCase naming, ignore null values, handle circular references. 
Configure custom converters for Guid, DateTime (ISO 8601), enums (string).
Location: src/Core/DotNetCloud.Core.Server/
```

**Deliverables:**
- ☐ JSON formatter (System.Text.Json)
- ☐ XML formatter (optional)
- ☐ camelCase naming policy
- ☐ Null value handling
- ☐ Custom converters (Guid, DateTime, enums)

**File Location:** `src/Core/DotNetCloud.Core.Server/`  
**Dependencies:** phase-0.7.11  
**Testing:** Serialization tests  
**Notes:** Flexible response formats

---

### Step: phase-0.7.13 - ETags & Conditional Requests
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Implement ETags for caching and concurrency control

**Recommended Prompt:**
```
Execute phase-0.7.13: Implement ETags. Generate ETags for GET responses (hash of content). 
Support If-None-Match (return 304 Not Modified if match), If-Match (return 412 Precondition Failed 
if no match). Use ETags for optimistic concurrency in PUT/PATCH. Store ETags in ETag response header.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/
```

**Deliverables:**
- ☐ ETag generation (content hash)
- ☐ If-None-Match support (304 responses)
- ☐ If-Match support (412 responses)
- ☐ Optimistic concurrency control
- ☐ ETag header in responses

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/`  
**Dependencies:** phase-0.7.12  
**Testing:** ETag tests  
**Notes:** Caching and concurrency

---

### Step: phase-0.7.14 - API Endpoint Documentation
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Create comprehensive API endpoint documentation

**Recommended Prompt:**
```
Execute phase-0.7.14: Document API endpoints. Add XML documentation comments (///) to all controller 
actions with summary, parameters, responses, examples. Use [ProducesResponseType] attributes. 
Document error codes, authentication requirements, rate limits. Create API reference markdown document.
Location: docs/api/
```

**Deliverables:**
- ☐ XML documentation on all actions
- ☐ [ProducesResponseType] attributes
- ☐ Error code documentation
- ☐ API reference markdown

**File Location:** `docs/api/`  
**Dependencies:** phase-0.7.13  
**Testing:** Documentation completeness check  
**Notes:** Developer experience

---

### Step: phase-0.7.15 - API Client SDK (C#)
**Status:** pending  
**Duration:** ~3 hours  
**Description:** Generate C# client SDK from OpenAPI schema

**Recommended Prompt:**
```
Execute phase-0.7.15: Create C# client SDK. Use NSwag to generate typed C# client from openapi.json. 
Create DotNetCloud.Client NuGet package with all API methods. Support dependency injection 
(AddDotNetCloudClient()). Handle authentication (bearer tokens). Include retry policies. 
Add XML documentation from API.
Location: src/Clients/DotNetCloud.Client/
```

**Deliverables:**
- ☐ NSwag code generation
- ☐ DotNetCloud.Client library
- ☐ Dependency injection support
- ☐ Authentication handling
- ☐ Retry policies
- ☐ XML documentation

**File Location:** `src/Clients/DotNetCloud.Client/`  
**Dependencies:** phase-0.7.6 (OpenAPI)  
**Testing:** Client SDK tests  
**Notes:** .NET developer experience

---

### Step: phase-0.7.16 - API Foundation Integration Tests
**Status:** pending  
**Duration:** ~2.5 hours  
**Description:** Create comprehensive API tests

**Recommended Prompt:**
```
Execute phase-0.7.16: Create API foundation tests. Test Kestrel configuration, CORS policies, 
rate limiting, response envelopes, pagination, filtering/sorting, ETags, model validation. 
Use WebApplicationFactory. Test error scenarios. Target 80%+ coverage.
Location: tests/DotNetCloud.Core.Server.Tests/Api/
```

**Deliverables:**
- ☐ Kestrel tests
- ☐ CORS tests
- ☐ Rate limiting tests
- ☐ Response envelope tests
- ☐ Pagination/filtering tests
- ☐ ETag tests
- ☐ Validation tests

**File Location:** `tests/DotNetCloud.Core.Server.Tests/Api/`  
**Dependencies:** phase-0.7.1 through phase-0.7.15  
**Testing:** 80%+ code coverage  
**Notes:** API reliability validation

---

## Section: Phase 0.8 - Real-Time Communication (SignalR)

### Step: phase-0.8.1 - SignalR Server Configuration
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Configure SignalR with WebSocket and long polling

**Recommended Prompt:**
```
Execute phase-0.8.1: Configure SignalR. Add SignalR services, configure WebSocket support, 
long polling fallback, messagepack protocol (binary). Set keep-alive interval (15s), 
timeout (30s), max concurrent connections per user. Configure CORS for SignalR.
Location: src/Core/DotNetCloud.Core.Server/
```

**Deliverables:**
- ☐ SignalR services configuration
- ☐ WebSocket support
- ☐ Long polling fallback
- ☐ MessagePack protocol
- ☐ Keep-alive and timeout configuration
- ☐ Connection limits

**File Location:** `src/Core/DotNetCloud.Core.Server/`  
**Dependencies:** phase-0.7.1 (Kestrel)  
**Testing:** SignalR connection tests  
**Notes:** Real-time foundation

---

### Step: phase-0.8.2 - Core SignalR Hub
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Create base SignalR hub with authentication

**Recommended Prompt:**
```
Execute phase-0.8.2: Create CoreHub. Implement base SignalR hub with [Authorize] attribute. 
Override OnConnectedAsync() to track user connections, OnDisconnectedAsync() to cleanup. 
Store connection IDs per user. Inject CallerContext from HttpContext.User claims. 
Create connection registry service.
Location: src/Core/DotNetCloud.Core.Server/Hubs/
```

**Deliverables:**
- ☐ `CoreHub` base class
- ☐ [Authorize] attribute
- ☐ OnConnectedAsync() implementation
- ☐ OnDisconnectedAsync() implementation
- ☐ Connection tracking per user
- ☐ `ConnectionRegistry` service

**File Location:** `src/Core/DotNetCloud.Core.Server/Hubs/`  
**Dependencies:** phase-0.8.1, phase-0.4.7 (authentication)  
**Testing:** Hub lifecycle tests  
**Notes:** Foundation for realtime features

---

### Step: phase-0.8.3 - Connection Tracking
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Track active SignalR connections per user

**Recommended Prompt:**
```
Execute phase-0.8.3: Create connection tracking. Implement ConnectionRegistry service that maps 
userId -> List<connectionId>. Store in memory (ConcurrentDictionary). Add connections on 
OnConnectedAsync, remove on OnDisconnectedAsync. Create methods: GetUserConnections(userId), 
IsUserOnline(userId), GetOnlineUsersCount(). Handle multi-device scenarios.
Location: src/Core/DotNetCloud.Core.Server/Services/
```

**Deliverables:**
- ☐ `ConnectionRegistry` service
- ☐ User to connection ID mapping
- ☐ In-memory storage (ConcurrentDictionary)
- ☐ GetUserConnections() method
- ☐ IsUserOnline() method
- ☐ GetOnlineUsersCount() method

**File Location:** `src/Core/DotNetCloud.Core.Server/Services/`  
**Dependencies:** phase-0.8.2  
**Testing:** Connection tracking tests  
**Notes:** Enables user presence

---

### Step: phase-0.8.4 - Reconnection Policies
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Configure automatic reconnection for clients

**Recommended Prompt:**
```
Execute phase-0.8.4: Configure reconnection. Set client reconnection policy (retry every 0s, 2s, 10s, 30s, 
then stop). Handle connection state changes (Connecting, Connected, Reconnecting, Disconnected). 
Persist connection state in client. Restore subscriptions on reconnect. Document client reconnection 
best practices.
Location: docs/api/
```

**Deliverables:**
- ☐ Reconnection policy configuration
- ☐ Retry intervals (0s, 2s, 10s, 30s)
- ☐ Connection state handling
- ☐ Subscription restoration on reconnect
- ☐ Client documentation

**File Location:** `docs/api/`  
**Dependencies:** phase-0.8.3  
**Testing:** Reconnection tests  
**Notes:** Improves reliability

---

### Step: phase-0.8.5 - IRealtimeBroadcaster Capability
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Implement realtime broadcast capability interface

**Recommended Prompt:**
```
Execute phase-0.8.5: Create IRealtimeBroadcaster. Implement capability interface with methods: 
Task BroadcastAsync(string group, object message), Task SendToUserAsync(Guid userId, object message), 
Task SendToRoleAsync(string role, object message), Task SendToConnectionAsync(string connectionId, 
object message). Implement using SignalR IHubContext<CoreHub>. Add to public tier capabilities.
Location: src/Core/DotNetCloud.Core/Capabilities/
```

**Deliverables:**
- ☐ `IRealtimeBroadcaster` interface
- ☐ BroadcastAsync() method
- ☐ SendToUserAsync() method
- ☐ SendToRoleAsync() method
- ☐ SendToConnectionAsync() method
- ☐ SignalR implementation

**File Location:** `src/Core/DotNetCloud.Core/Capabilities/`  
**Dependencies:** phase-0.8.4, phase-0.1.1 (capabilities)  
**Testing:** Broadcast tests  
**Notes:** Module real-time communication

---

### Step: phase-0.8.6 - Group Management
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Implement SignalR group/channel management

**Recommended Prompt:**
```
Execute phase-0.8.6: Create group management. Implement methods in CoreHub: JoinGroupAsync(string groupName), 
LeaveGroupAsync(string groupName). Track group memberships per connection. Create GroupRegistry service 
mapping groupName -> List<connectionId>. Support multiple groups per connection. Use for chat rooms, 
notifications.
Location: src/Core/DotNetCloud.Core.Server/Hubs/
```

**Deliverables:**
- ☐ JoinGroupAsync() method
- ☐ LeaveGroupAsync() method
- ☐ `GroupRegistry` service
- ☐ Group membership tracking
- ☐ Multiple groups per connection

**File Location:** `src/Core/DotNetCloud.Core.Server/Hubs/`  
**Dependencies:** phase-0.8.5  
**Testing:** Group management tests  
**Notes:** Foundation for channels/rooms

---

### Step: phase-0.8.7 - Presence Tracking
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Implement user presence (online/offline/away)

**Recommended Prompt:**
```
Execute phase-0.8.7: Create presence tracking. Implement PresenceService tracking user status 
(Online, Away, Offline), LastSeenAt timestamp. Update status on connect/disconnect. 
Set Away after inactivity (5 min default). Create hub method UpdatePresenceAsync(PresenceStatus status). 
Broadcast presence changes to relevant users. Store in memory + UserDevice table.
Location: src/Core/DotNetCloud.Core.Server/Services/
```

**Deliverables:**
- ☐ `PresenceService`
- ☐ PresenceStatus enum (Online/Away/Offline)
- ☐ LastSeenAt tracking
- ☐ Inactivity timeout (Away)
- ☐ UpdatePresenceAsync() hub method
- ☐ Presence change broadcasts

**File Location:** `src/Core/DotNetCloud.Core.Server/Services/`  
**Dependencies:** phase-0.8.6, phase-0.2.6 (UserDevice)  
**Testing:** Presence tests  
**Notes:** User status visibility

---

### Step: phase-0.8.8 - Typing Indicators
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Implement typing indicator broadcast

**Recommended Prompt:**
```
Execute phase-0.8.8: Create typing indicators. Implement hub method TypingAsync(string groupName) 
that broadcasts "user is typing" to group members (exclude sender). Auto-clear after 3 seconds 
(configurable). Throttle broadcasts (max 1 per second per user). Use for chat, comments. 
Create client-side helper documentation.
Location: src/Core/DotNetCloud.Core.Server/Hubs/
```

**Deliverables:**
- ☐ TypingAsync() hub method
- ☐ Broadcast to group members
- ☐ Auto-clear after timeout (3s)
- ☐ Throttling (1 per second)
- ☐ Client helper documentation

**File Location:** `src/Core/DotNetCloud.Core.Server/Hubs/`  
**Dependencies:** phase-0.8.7  
**Testing:** Typing indicator tests  
**Notes:** UX enhancement

---

### Step: phase-0.8.9 - SignalR Authentication
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Integrate bearer token authentication for SignalR

**Recommended Prompt:**
```
Execute phase-0.8.9: Configure SignalR authentication. Accept bearer token via query parameter 
(access_token=<token>) for WebSocket handshake. Validate token, extract CallerContext. 
Reject invalid tokens with 401. Support token refresh during long-lived connections. 
Document authentication for SignalR clients.
Location: src/Core/DotNetCloud.Core.Server/
```

**Deliverables:**
- ☐ Query parameter token authentication
- ☐ Token validation
- ☐ CallerContext extraction
- ☐ 401 on invalid token
- ☐ Token refresh support
- ☐ Client documentation

**File Location:** `src/Core/DotNetCloud.Core.Server/`  
**Dependencies:** phase-0.4.7 (token validation)  
**Testing:** SignalR auth tests  
**Notes:** Secure real-time connections

---

### Step: phase-0.8.10 - Message Deduplication
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Implement message deduplication for reliability

**Recommended Prompt:**
```
Execute phase-0.8.10: Create message deduplication. Assign unique messageId (Guid) to each broadcast. 
Track sent messageIds per connection (in-memory cache, 1-hour expiration). Skip duplicate sends. 
Handle race conditions. Useful for ensuring exactly-once delivery semantics. Document in API.
Location: src/Core/DotNetCloud.Core.Server/Services/
```

**Deliverables:**
- ☐ Message ID generation (Guid)
- ☐ Sent message tracking (in-memory cache)
- ☐ Duplicate detection
- ☐ Race condition handling
- ☐ Documentation

**File Location:** `src/Core/DotNetCloud.Core.Server/Services/`  
**Dependencies:** phase-0.8.9  
**Testing:** Deduplication tests  
**Notes:** Exactly-once delivery

---

### Step: phase-0.8.11 - SignalR Integration Tests
**Status:** pending  
**Duration:** ~2.5 hours  
**Description:** Create comprehensive SignalR tests

**Recommended Prompt:**
```
Execute phase-0.8.11: Create SignalR tests. Test hub connection/disconnection, authentication, 
group join/leave, presence tracking, typing indicators, broadcasts (user/role/group), message 
deduplication. Use Microsoft.AspNetCore.SignalR.Client for tests. Test reconnection scenarios. 
Target 80%+ coverage.
Location: tests/DotNetCloud.Core.Server.Tests/Hubs/
```

**Deliverables:**
- ☐ Connection lifecycle tests
- ☐ Authentication tests
- ☐ Group management tests
- ☐ Presence tests
- ☐ Broadcast tests
- ☐ Deduplication tests
- ☐ Reconnection tests

**File Location:** `tests/DotNetCloud.Core.Server.Tests/Hubs/`  
**Dependencies:** phase-0.8.1 through phase-0.8.10  
**Testing:** 80%+ code coverage  
**Notes:** Real-time reliability validation

---

## Section: Phase 0.9 - Authentication API Endpoints

**NOTE:** Phase 0.9 endpoints are implemented as part of Phase 0.4 controllers. This section documents the endpoint structure for reference.

### Step: phase-0.9.1 - User Authentication Endpoints
**Status:** pending (implemented in phase-0.4.9, phase-0.4.10)  
**Duration:** Included in Phase 0.4  
**Description:** REST endpoints for user registration, login, logout

**Deliverables:**
- ☐ POST /api/v1/core/auth/register
- ☐ POST /api/v1/core/auth/login
- ☐ POST /api/v1/core/auth/logout
- ☐ POST /api/v1/core/auth/refresh
- ☐ GET /api/v1/core/auth/user

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs`  
**Dependencies:** phase-0.4.9, phase-0.4.10  
**Testing:** Covered in phase-0.4.20  
**Notes:** Core authentication endpoints

---

### Step: phase-0.9.2 - OAuth2/OIDC Integration Endpoints
**Status:** pending (implemented in phase-0.4.2, phase-0.4.3, phase-0.4.4)  
**Duration:** Included in Phase 0.4  
**Description:** OAuth2/OIDC endpoints for external provider sign-in

**Deliverables:**
- ☐ GET /api/v1/core/auth/external-login/{provider}
- ☐ GET /api/v1/core/auth/external-callback
- ☐ GET /.well-known/openid-configuration

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs`  
**Dependencies:** phase-0.4.15, phase-0.4.16, phase-0.4.19  
**Testing:** Covered in phase-0.4.20  
**Notes:** External authentication integration

---

### Step: phase-0.9.3 - MFA Management Endpoints
**Status:** pending (implemented in phase-0.4.11, phase-0.4.12)  
**Duration:** Included in Phase 0.4  
**Description:** MFA setup and verification endpoints

**Deliverables:**
- ☐ POST /api/v1/core/auth/mfa/totp/setup
- ☐ POST /api/v1/core/auth/mfa/totp/verify
- ☐ POST /api/v1/core/auth/mfa/passkey/setup
- ☐ POST /api/v1/core/auth/mfa/passkey/verify
- ☐ GET /api/v1/core/auth/mfa/backup-codes

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs`  
**Dependencies:** phase-0.4.11, phase-0.4.12  
**Testing:** Covered in phase-0.4.20  
**Notes:** Multi-factor authentication

---

### Step: phase-0.9.4 - Password Management Endpoints
**Status:** pending (implemented in phase-0.4.10)  
**Duration:** Included in Phase 0.4  
**Description:** Password change and reset endpoints

**Deliverables:**
- ☐ POST /api/v1/core/auth/password/change
- ☐ POST /api/v1/core/auth/password/forgot
- ☐ POST /api/v1/core/auth/password/reset

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs`  
**Dependencies:** phase-0.4.10  
**Testing:** Covered in phase-0.4.20  
**Notes:** Password lifecycle management

---

### Step: phase-0.9.5 - Device Management Endpoints
**Status:** pending (implemented in phase-0.4.14)  
**Duration:** Included in Phase 0.4  
**Description:** User device tracking and management endpoints

**Deliverables:**
- ☐ GET /api/v1/core/auth/devices
- ☐ DELETE /api/v1/core/auth/devices/{deviceId}

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs`  
**Dependencies:** phase-0.4.14  
**Testing:** Covered in phase-0.4.20  
**Notes:** Device security management

---

**(Continue for remaining phases 0.10-0.19...)**

Due to length constraints, the full document continues with phases 0.10-0.19 following the same detailed format. Would you like me to continue adding the remaining phases?
