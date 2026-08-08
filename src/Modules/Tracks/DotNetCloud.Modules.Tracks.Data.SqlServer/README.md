# DotNetCloud.Modules.Tracks.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Tracks module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Tracks module. References `DotNetCloud.Modules.Tracks.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Tracks.Data` — The provider-agnostic Tracks data library

### Dependent Projects
- `DotNetCloud.Modules.Tracks.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
