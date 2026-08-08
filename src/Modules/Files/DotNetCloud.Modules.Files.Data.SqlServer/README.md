# DotNetCloud.Modules.Files.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Files module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Files module. A thin project referencing `DotNetCloud.Modules.Files.Data` containing only the design-time factory and migration files.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Files.Data` — The provider-agnostic Files data library

### Dependent Projects
- `DotNetCloud.Modules.Files.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
