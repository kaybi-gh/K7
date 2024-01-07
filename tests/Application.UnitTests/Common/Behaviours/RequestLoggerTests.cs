using MediaServer.Application.Common.Behaviours;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Libraries.Commands.CreateLibrary;
using MediaServer.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaServer.Application.UnitTests.Common.Behaviours;

public class RequestLoggerTests
{
    private Mock<ILogger<CreateLibraryCommand>> _logger = null!;
    private Mock<IUser> _user = null!;
    private Mock<IIdentityService> _identityService = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new Mock<ILogger<CreateLibraryCommand>>();
        _user = new Mock<IUser>();
        _identityService = new Mock<IIdentityService>();
    }

    [Test]
    public async Task ShouldCallGetUserNameAsyncOnceIfAuthenticated()
    {
        // Arrange
        _user.Setup(x => x.Id).Returns(Guid.NewGuid().ToString());
        var requestLogger = new LoggingBehaviour<CreateLibraryCommand>(_logger.Object, _user.Object, _identityService.Object);
        
        // Act
        await requestLogger.Process(new CreateLibraryCommand {
            MediaType = LibraryMediaType.Music,
            Title = "title",
            RootPath = "path"
        }, new CancellationToken());

        // Assert
        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task ShouldNotCallGetUserNameAsyncOnceIfUnauthenticated()
    {
        // Arrange
        var requestLogger = new LoggingBehaviour<CreateLibraryCommand>(_logger.Object, _user.Object, _identityService.Object);

        // Act
        await requestLogger.Process(new CreateLibraryCommand {
            MediaType = LibraryMediaType.Music,
            Title = "title",
            RootPath = "path"
        }, new CancellationToken());
        
        // Assert
        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Never);
    }
}
