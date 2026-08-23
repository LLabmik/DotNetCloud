# DotNetCloud.Integration.Tests.SqlServer

> **Purpose:** Integration tests targeting SQL Server — validates the platform with Microsoft SQL Server
> **Type:** Test Project (Integration)
> **Target Framework:** net10.0

## Overview

Integration tests that validate the DotNetCloud platform using SQL Server as the database provider. Ensures SQL Server naming conventions, migration compatibility, and query behavior are correct.

## Projects Under Test

- `DotNetCloud.Core.Data` — Core data layer (SQL Server mode)
- All module `.Data.SqlServer` projects — SQL Server migration assemblies

## Test Framework

MSTest with SQL Server database fixture.
