# DotNetCloud.Modules.Notes.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Notes module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Notes module. References `DotNetCloud.Modules.Notes.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Notes.Data` — The provider-agnostic Notes data library

### Dependent Projects
- `DotNetCloud.Modules.Notes.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
