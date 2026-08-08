# DotNetCloud.Modules.Calendar.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Calendar module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Calendar module. References `DotNetCloud.Modules.Calendar.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Calendar.Data` — The provider-agnostic Calendar data library

### Dependent Projects
- `DotNetCloud.Modules.Calendar.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
