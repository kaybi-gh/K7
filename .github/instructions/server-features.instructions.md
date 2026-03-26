---
applyTo: "src/Server/Application/**"
---

# Server Application Layer Instructions

## Feature Folder Structure

Each feature lives under `Features/{Feature}/` with this layout:

```
Features/
  Libraries/
    Commands/
      CreateLibrary/
        CreateLibrary.cs                  ← Request record + Handler class
        CreateLibraryCommandValidator.cs  ← FluentValidation
      UpdateLibrary/
        UpdateLibrary.cs
        UpdateLibraryCommandValidator.cs
      DeleteLibrary/
        DeleteLibrary.cs
    Queries/
      GetLibraries/
        GetLibraries.cs
      GetLibrary/
        GetLibrary.cs
    EventHandlers/
      LibraryCreatedEventHandler.cs
      LibraryDeletedEventHandler.cs
```

## Command Pattern

Request record and handler class in the **same file**. Handler uses traditional constructor injection with `private readonly` fields:

```csharp
// Good: Request + Handler in same file
[Authorize(Roles = Roles.Administrator)]
public record CreateLibraryCommand : IRequest<Guid>
{
    public required string Title { get; init; }
    public required string RootPath { get; init; }
}

public class CreateLibraryCommandHandler : IRequestHandler<CreateLibraryCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateLibraryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateLibraryCommand request, CancellationToken cancellationToken)
    {
        var entity = new Library { Title = request.Title, RootPath = request.RootPath };
        entity.AddDomainEvent(new LibraryCreatedEvent(entity));
        _context.Libraries.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

// Avoid: Separate files for request and handler
// Avoid: Returning Result<T> wrappers — throw typed exceptions instead
```

## Query Pattern

Same structure as commands but read-only. Always use `AsNoTracking()`:

```csharp
// Good: Query with AsNoTracking
public record GetLibrariesQuery : IRequest<IEnumerable<Library>>;

public class GetLibrariesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLibrariesQuery, IEnumerable<Library>>
{
    public async Task<IEnumerable<Library>> Handle(GetLibrariesQuery request, CancellationToken cancellationToken)
    {
        return await context.Libraries
            .AsNoTracking()
            .OrderBy(l => l.Title)
            .ToListAsync(cancellationToken);
    }
}
```

## Validation

FluentValidation in a companion file named `{Name}CommandValidator.cs`:

```csharp
public class CreateLibraryCommandValidator : AbstractValidator<CreateLibraryCommand>
{
    public CreateLibraryCommandValidator(IApplicationDbContext context)
    {
        RuleFor(v => v.Title).NotEmpty().MaximumLength(200);
        RuleFor(v => v.RootPath).NotEmpty();
    }
}
```

The `ValidationBehaviour` pipeline intercepts validation failures and throws `ValidationException` — no manual validation in handlers.

## Error Handling

Throw typed exceptions. The `CustomExceptionHandler` middleware maps them to HTTP responses:

```csharp
// Good: Throw typed exception
throw new NotFoundException(nameof(Library), request.Id);

// Avoid: Returning error codes or Result<T>
```

## Domain Events

Raise in entity, handle in `EventHandlers/` subfolder:

```csharp
entity.AddDomainEvent(new LibraryCreatedEvent(entity));
```

## Data Access

- Use `IApplicationDbContext` for database operations.
- Use `ISender` to dispatch other commands/queries from within a handler.

## CancellationToken

- Always forward `CancellationToken` to async calls.
- Always last parameter.
- Public methods: `CancellationToken cancellationToken = default`.

## Element Ordering

Within a class, order members as:
1. Fields
2. Constructors
3. Delegates
4. Events
5. Properties
6. Methods
