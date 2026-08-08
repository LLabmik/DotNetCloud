# DotNetCloud.Modules.AI.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the AI module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the AI module. References `DotNetCloud.Modules.AI.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.AI.Data` — The provider-agnostic AI data library

### Dependent Projects
- `DotNetCloud.Modules.AI.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
