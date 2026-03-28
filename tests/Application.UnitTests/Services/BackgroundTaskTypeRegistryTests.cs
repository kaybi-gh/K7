using K7.Server.Application.Services;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Services;

public class BackgroundTaskTypeRegistryTests
{
    private BackgroundTaskTypeRegistry _registry;

    [SetUp]
    public void Setup()
    {
        var logger = Substitute.For<ILogger<BackgroundTaskTypeRegistry>>();
        _registry = new BackgroundTaskTypeRegistry(logger);
    }

    [Test]
    public void Resolve_ShouldReturnType_WhenTypeIsKnownRequest()
    {
        // Arrange
        var typeName = "K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask.CreateBackgroundTaskCommand";

        // Act
        var result = _registry.Resolve(typeName);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("CreateBackgroundTaskCommand");
    }

    [Test]
    public void Resolve_ShouldReturnNull_WhenTypeIsUnknown()
    {
        // Act
        var result = _registry.Resolve("Some.Unknown.Type");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Resolve_ShouldReturnNull_ForEmptyString()
    {
        // Act
        var result = _registry.Resolve(string.Empty);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Resolve_ShouldReturnNull_ForAssemblyQualifiedName()
    {
        // Assembly-qualified names should not be accepted (old format)
        var typeName = "K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask.CreateBackgroundTaskCommand, K7.Server.Application, Version=1.0.0.0";

        // Act
        var result = _registry.Resolve(typeName);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Constructor_ShouldDiscoverRequestTypes()
    {
        // The registry should find at least some IBaseRequest implementations
        // (e.g., CreateBackgroundTaskCommand, DeleteBackgroundTaskCommand, etc.)
        var knownType = "K7.Server.Application.Features.BackgroundTasks.Commands.DeleteBackgroundTask.DeleteBackgroundTaskCommand";

        // Act
        var result = _registry.Resolve(knownType);

        // Assert
        result.Should().NotBeNull();
    }
}
