# DotNetCloud.Modules.Contacts.Data.SqlServer

> **Purpose:** SQL Server EF Core migration assembly for the Contacts module
> **Type:** Migration Assembly
> **Target Framework:** net10.0

## Overview

Holds the SQL Server-specific EF Core migrations for the Contacts module. References `DotNetCloud.Modules.Contacts.Data`.

## Projects This Interacts With

### Direct Dependencies
- `DotNetCloud.Modules.Contacts.Data` — The provider-agnostic Contacts data library

### Dependent Projects
- `DotNetCloud.Modules.Contacts.Host` — Runtime migration discovery
- `DotNetCloud.Core.Server` — Runtime Assembly.Load resolution
- `DotNetCloud.CLI` — `dotnetcloud migrate` command
