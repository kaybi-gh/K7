---
applyTo: "src/Server/Domain/**"
---

# Domain Layer Instructions

## Core Principle

The Domain layer has **zero dependencies** on other projects. It defines the business model — entities, value objects, enums, events, constants, and interfaces.

## Entities

All entities inherit `BaseEntity`, which provides an `Id` (Guid) and domain event support:

```csharp
// Good: Entity inheriting BaseEntity
public class Library : BaseEntity
{
    public required string Title { get; set; }
    public required string RootPath { get; set; }
    public LibraryMediaType MediaType { get; set; }
}

// Avoid: Entity without BaseEntity inheritance
// Avoid: Business logic that depends on infrastructure
```

## Domain Events

Entities raise events via `AddDomainEvent()`. Events inherit `BaseEvent`. They are dispatched automatically by an EF Core `SaveChangesInterceptor`:

```csharp
// Good: Raising a domain event
public class LibraryCreatedEvent(Library library) : BaseEvent
{
    public Library Library { get; } = library;
}

// In entity code:
entity.AddDomainEvent(new LibraryCreatedEvent(entity));
```

## Project Structure

```
Domain/
  Common/          ← BaseEntity, BaseEvent, shared helpers
  Constants/       ← Roles, Regexes, string constants
  Entities/        ← Domain entities
  Enums/           ← Domain enumerations
  Events/          ← Domain event classes
  Exceptions/      ← Domain-specific exceptions
  Interfaces/      ← Repository and service interfaces consumed by Application
  ValueObjects/    ← Value objects (immutable, equality by value)
```

## Interfaces

Define interfaces for repositories and services that the Application layer needs. Infrastructure implements them:

```csharp
// Good: Interface in Domain, implemented in Infrastructure
public interface IApplicationDbContext
{
    DbSet<Library> Libraries { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```

## Element Ordering

Within a class: fields → constructors → delegates → events → properties → methods.
