---
applyTo: "src/Server/Domain/**"
---

# Domain layer

Follow [docs/dev/architecture.md](../../docs/dev/architecture.md) and [AGENTS.md](../../AGENTS.md).

Summary: zero dependencies; entities inherit `BaseEntity`; events via `AddDomainEvent()`. Service contracts in Domain; `IApplicationDbContext` lives in Application.
