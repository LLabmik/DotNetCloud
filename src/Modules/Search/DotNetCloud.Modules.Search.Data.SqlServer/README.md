# DotNetCloud.Modules.Search.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Search module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Search module. References `DotNetCloud.Modules.Search.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Search.Data` — The provider-agnostic Search data library

### Dependent Projects
- `DotNetCloud.Modules.Search.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
