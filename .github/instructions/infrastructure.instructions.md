---
applyTo: "src/Server/Infrastructure/**"
---

# Infrastructure Layer Instructions

## Role

Implements interfaces defined in Domain/Application. Houses external concerns: database, file system, media processing, configuration.

## Sub-Projects

| Project | Role |
|---|---|
| `Database/Context` | `ApplicationDbContext`, EF Core entity configurations, interceptors |
| `Database/Providers/Postgres` | Postgres-specific provider, migrations |
| `Database/Providers/Sqlite` | Sqlite-specific provider, migrations |
| `FileSystem` | File system abstraction and access |
| `MediaProcessing` | ffmpeg, Essentia, transcoding, image extraction |
| `Configuration` | Configuration binding, options classes |

## EF Core Entity Configuration

Use `IEntityTypeConfiguration<T>` for each entity. Place in `Database/Context/Data/Configurations/`:

```csharp
// Good: Fluent configuration in dedicated class
public class LibraryConfiguration : IEntityTypeConfiguration<Library>
{
    public void Configure(EntityTypeBuilder<Library> builder)
    {
        builder.Property(l => l.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(l => l.RootPath).IsUnique();
    }
}

// Avoid: Configuration in OnModelCreating directly
```

## DI Registration

Each sub-project has a `DependencyInjection.cs` with an `Add*Services()` extension method:

```csharp
// Good: Lambda-based service resolution
services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var config = serviceProvider.GetRequiredService<IOptions<DatabaseConfiguration>>().Value;
    options.UseNpgsql(config.ConnectionString);
});

// Avoid: Building a temporary service provider
var provider = services.BuildServiceProvider(); // NEVER do this
var config = provider.GetRequiredService<IOptions<DatabaseConfiguration>>();
```

## Migrations

```bash
# Postgres
dotnet ef migrations add <Name> \
  --project ./src/Server/Infrastructure/Database/Providers/Postgres \
  --startup-project ./src/Server/Web \
  -- --Database:Provider Postgres

# Sqlite
dotnet ef migrations add <Name> \
  --project ./src/Server/Infrastructure/Database/Providers/Sqlite \
  --startup-project ./src/Server/Web \
  -- --Database:Provider Sqlite
```

## Domain Event Dispatch

`DispatchDomainEventsInterceptor` fires domain events during `SaveChangesAsync`. This is already wired — no manual dispatch needed.
