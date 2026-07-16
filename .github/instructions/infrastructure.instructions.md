---
applyTo: "src/Server/Infrastructure/**"
---

# Infrastructure layer

Follow [docs/dev/architecture.md](../../docs/dev/architecture.md) (infrastructure split) and [CONTRIBUTING.md - Migrations](../../CONTRIBUTING.md#migrations).

Summary: `IEntityTypeConfiguration<T>` under `Database/Context/Data/Configurations/`; lambda DI only (never `BuildServiceProvider()`); migrations for both Postgres and Sqlite.
