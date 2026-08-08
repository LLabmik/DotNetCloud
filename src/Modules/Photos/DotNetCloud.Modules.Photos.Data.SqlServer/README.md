# DotNetCloud.Modules.Photos.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Photos module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Photos module. References `DotNetCloud.Modules.Photos.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Photos.Data` — The provider-agnostic Photos data library

### Dependent Projects
- `DotNetCloud.Modules.Photos.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
