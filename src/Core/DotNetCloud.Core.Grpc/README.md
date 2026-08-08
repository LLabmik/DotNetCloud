# DotNetCloud.Core.Grpc

> **Purpose:** Shared gRPC infrastructure and protocol definitions for inter-module communication
> **Type:** Library
> **Target Framework:** net10.0

## Overview

`DotNetCloud.Core.Grpc` provides the foundational gRPC protocol definitions and service contracts used by all DotNetCloud modules and the core server. It defines the proto schemas for module lifecycle management, capability negotiation, and token introspection — the three mandatory gRPC services every process-isolated module must implement.

All module-to-core and module-to-module communication flows through gRPC over Unix sockets (Linux) or Named Pipes (Windows). This project is the single source of truth for the shared proto contracts.

## Key Features

- **Module Lifecycle Protocol** — `module_lifecycle.proto`: Initialize, Start, Stop, HealthCheck RPCs every module host implements
- **Capability Negotiation Protocol** — `module_capabilities.proto`: Declare required capabilities, validate grants at module startup
- **Token Introspection Protocol** — `token_introspection.proto`: Validate JWT bearer tokens for gRPC inter-module authentication
- **gRPC Service & Client Generation** — Proto files compiled with `GrpcServices="Both"` to generate both server stubs and client proxies
- **Channel Management Primitives** — Shared base classes used by `ProcessSupervisor` in `DotNetCloud.Core.Server`

## Projects This Interacts With

### Direct Dependencies (Project References)
- `DotNetCloud.Core` — Core interfaces and DTOs used in gRPC message serialization

### Dependent Projects (Projects that reference this one)
- `DotNetCloud.Core.Server` — Uses generated gRPC clients to communicate with module hosts
- `DotNetCloud.Core.Auth` — Uses token introspection proto for gRPC auth validation
- Every `*.Host.csproj` module — Implements the lifecycle and capability gRPC services defined here

## Key Files

| File | Purpose |
|------|---------|
| `Protos/module_lifecycle.proto` | Module lifecycle RPCs: InitializeAsync, StartAsync, StopAsync, HealthCheck |
| `Protos/module_capabilities.proto` | Capability declaration and validation RPCs |
| `Protos/token_introspection.proto` | JWT bearer token validation for gRPC calls |
