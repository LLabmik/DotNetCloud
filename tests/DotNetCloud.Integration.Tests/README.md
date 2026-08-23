# DotNetCloud.Integration.Tests

> **Purpose:** Integration tests for the DotNetCloud platform — end-to-end module communication and API testing
> **Type:** Test Project (Integration)
> **Target Framework:** net10.0

## Overview

End-to-end integration tests that validate the full DotNetCloud stack: server startup, module process isolation, gRPC communication between modules, REST API endpoints, authentication flows, and database operations. Uses the default database provider.

## Projects Under Test

- `DotNetCloud.Core.Server` — Main server host
- All module `.Host` projects — Process-isolated module hosts
- All module `.Data` projects — Data access layers

## Test Framework

MSTest with HTTP client for API testing.
