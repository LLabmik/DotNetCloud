# DotNetCloud.Core.Server.Tests

> **Purpose:** Unit tests for `DotNetCloud.Core.Server` — process supervisor, module loading, and gRPC routing
> **Type:** Test Project
> **Target Framework:** net10.0

## Overview

Tests covering the core server: process supervisor lifecycle, module discovery and loading, gRPC channel management, health check aggregation, and middleware pipeline.

## Project Under Test

- `DotNetCloud.Core.Server` — Main server host process

## Test Framework

MSTest with Moq for mocking.
