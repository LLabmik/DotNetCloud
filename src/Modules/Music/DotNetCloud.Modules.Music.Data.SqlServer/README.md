# DotNetCloud.Modules.Music.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Music module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Music module. References `DotNetCloud.Modules.Music.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Music.Data` — The provider-agnostic Music data library

### Dependent Projects
- `DotNetCloud.Modules.Music.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
