---
applyTo: "src/Server/Application/**"
---

# Application layer

Follow [docs/dev/architecture.md](../../docs/dev/architecture.md), [CONTRIBUTING.md - Adding a new feature](../../CONTRIBUTING.md#adding-a-new-feature), and [AGENTS.md](../../AGENTS.md).

Summary: CQRS under `Features/{Feature}/Commands|Queries/{Name}/`; request + handler same file; FluentValidation companion; throw typed exceptions (no `Result<T>`); queries use `AsNoTracking()`.
