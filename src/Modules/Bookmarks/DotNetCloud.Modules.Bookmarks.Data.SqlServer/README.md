# DotNetCloud.Modules.Bookmarks.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Bookmarks module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Bookmarks module. References `DotNetCloud.Modules.Bookmarks.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Bookmarks.Data` — The provider-agnostic Bookmarks data library

### Dependent Projects
- `DotNetCloud.Modules.Bookmarks.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
