# DotNetCloud.Modules.Email.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Email module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Email module. References `DotNetCloud.Modules.Email.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Email.Data` — The provider-agnostic Email data library

### Dependent Projects
- `DotNetCloud.Modules.Email.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
