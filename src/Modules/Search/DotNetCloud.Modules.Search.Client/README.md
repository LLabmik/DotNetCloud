# DotNetCloud.Modules.Search.Client

> **Purpose:** Search module gRPC client library — typed gRPC client for other modules to integrate with search
> **Type:** Library
> **Target Framework:** net10.0

## Overview

A lightweight gRPC client library that other DotNetCloud modules reference to integrate with the Search module. Provides a typed client interface for registering as a search provider, pushing index updates, and querying the search index. Isolates the gRPC dependency so other modules don't need to reference proto files directly.

## Key Features

- **Typed gRPC Client** — Strongly-typed client for Search gRPC services
- **Search Provider Registration** — API for modules to register as searchable
- **Index Updates** — Push updates to the search index from module hosts
- **Query Interface** — Simple API for modules to query the unified search index

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Core` — Core interfaces
- `DotNetCloud.Core.Grpc` — gRPC infrastructure

### Dependent Projects
- Other module `.Host` projects — Use this client to integrate with search
