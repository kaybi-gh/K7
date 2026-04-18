---
applyTo: "tests/**"
---

# Testing Instructions

## Stack

- **NUnit** - test framework
- **FluentAssertions** - assertion library
- **NSubstitute** - mocking

## Test Project Structure

| Project | Tests | References |
|---|---|---|
| `Domain.UnitTests` | Domain entities, value objects, business rules | Domain |
| `Application.UnitTests` | Handlers, validators, mappings | Application, Domain |
| `Application.FunctionalTests` | Full request pipeline via `WebApplicationFactory` | All server layers |
| `Infrastructure.IntegrationTests` | Database, file system, media processing | Infrastructure, Domain |
| `Tests.Helpers` | Shared fixtures, factories, sample data | All test projects |

## Test Structure - Arrange / Act / Assert

```csharp
// Good: Clear AAA structure
[Test]
public async Task Handle_ShouldCreateLibrary_WhenCommandIsValid()
{
    // Arrange
    var context = CreateDbContext();
    var handler = new CreateLibraryCommandHandler(context, Substitute.For<ISender>());
    var command = new CreateLibraryCommand { Title = "Music", RootPath = "/media/music", MediaType = LibraryMediaType.Music };

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert
    result.Should().NotBeEmpty();
    var library = await context.Libraries.FindAsync(result);
    library.Should().NotBeNull();
    library!.Title.Should().Be("Music");
}
```

## Integration Test Infrastructure

- **`CustomWebApplicationFactory`**: Configures in-memory test server with test database.
- **Testcontainers**: Spins up a real PostgreSQL container for integration tests.
- **Respawn**: Resets database state between tests (faster than recreating).

```csharp
// Good: Using fixture for database tests
[TestFixture]
public class LibraryTests : DatabaseFixture
{
    [Test]
    public async Task Should_Persist_Library()
    {
        // Uses the shared database context from fixture
    }
}
```

## Frontend Component Tests (bUnit)

For critical shared Blazor components in `Clients/Shared/Components`:

```csharp
// Good: bUnit component test
[Test]
public void MediaPoster_ShouldRenderTitle()
{
    using var ctx = new BunitContext();
    var cut = ctx.Render<MediaPoster>(parameters =>
        parameters.Add(p => p.Title, "Test Movie"));

    cut.Find(".media-title").TextContent.Should().Be("Test Movie");
}
```

## Naming Conventions

- Test classes: `{ClassUnderTest}Tests`
- Test methods: `{Method}_Should{Expected}_When{Condition}`
- Test projects: `{Layer}.{TestType}Tests`
