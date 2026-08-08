# DotNetCloud.Modules.Video.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Video module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Video module. References `DotNetCloud.Modules.Video.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Video.Data` — The provider-agnostic Video data library

### Dependent Projects
- `DotNetCloud.Modules.Video.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
