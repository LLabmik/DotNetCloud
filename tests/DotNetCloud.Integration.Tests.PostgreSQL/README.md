# DotNetCloud.Integration.Tests.PostgreSQL

> **Purpose:** Integration tests targeting PostgreSQL — validates the platform with PostgreSQL
> **Type:** Test Project (Integration)
> **Target Framework:** net10.0

## Overview

Integration tests that validate the DotNetCloud platform using PostgreSQL as the database provider. Ensures PostgreSQL naming conventions (snake_case schemas), migration compatibility, and query behavior are correct.

## Projects Under Test

- `DotNetCloud.Core.Data` — Core data layer (PostgreSQL mode)
- All module `.Data` projects — PostgreSQL migrations

## Test Framework

MSTest with PostgreSQL database fixture.
