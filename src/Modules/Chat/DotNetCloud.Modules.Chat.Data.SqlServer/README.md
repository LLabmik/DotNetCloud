# DotNetCloud.Modules.Chat.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Chat module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Chat module. A thin project referencing `DotNetCloud.Modules.Chat.Data` containing only the design-time factory and migration files.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Chat.Data` — The provider-agnostic Chat data library

### Dependent Projects
- `DotNetCloud.Modules.Chat.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
