# Changelog

All notable changes to the Example module are documented here.

## 1.0.2

- Fix: the sandboxed host could not self-migrate. The model and the migration named a schema
  (`example`), while a sandboxed module's principal owns only its generated schema and has no
  `CREATE` privilege on the database, so `MigrateAsync` failed with PostgreSQL `42501 permission
  denied for database` and the module never created its tables. The model, both provider migrations
  and the migration-history table are now schema-neutral; the provisioned connection string's
  `search_path` decides where they land. `ExampleDbContextModelTests` guards both the
  model/snapshot match and the absence of schema-qualified DDL.
- Supersedes 1.0.1, which shipped the model/snapshot mismatch only.

## 1.0.1

- Fix: the sandboxed host could not self-migrate. The model's default schema is the store's
  generated schema at runtime, while the migration snapshot records the logical `example` schema, so
  EF reported pending model changes and `MigrateAsync` refused to run (the module exited on startup
  with nothing created). The host now suppresses only
  `RelationalEventId.PendingModelChangesWarning`, and `ExampleDbContextModelTests` guards real
  model/migration drift against the design-time schema.

## 1.0.0

- Reference implementation of a DotNetCloud module: lifecycle, capabilities, events, gRPC service,
  settings and Blazor UI components.
