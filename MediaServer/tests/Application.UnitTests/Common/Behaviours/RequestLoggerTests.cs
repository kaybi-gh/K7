using MediaServer.Application.Common.Behaviours;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Features.Libraries.Commands.CreateLibrary;
using MediaServer.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.UnitTests.Common.Behaviours;

public class RequestLoggerTests
{
    private ILogger<CreateLibraryCommand> _logger;
    private IUser _user;
    private IIdentityService _identityService;

    [SetUp]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<CreateLibraryCommand>>();
        _user = Substitute.For<IUser>();
        _identityService = Substitute.For<IIdentityService>();
    }

    [Test]
    public async Task ShouldCallGetUserNameAsyncOnceIfAuthenticated()
    {
        // Arrange
        _user.Id.Returns(Guid.NewGuid().ToString());
        var requestLogger = new LoggingBehaviour<CreateLibraryCommand>(_logger, _user, _identityService);

        // Act
        await requestLogger.Process(new CreateLibraryCommand
        {
            MediaType = LibraryMediaType.Music,
            Title = "title",
            RootPath = "path"
        }, new CancellationToken());

        // Assert
        await _identityService.Received(1).GetUserNameAsync(Arg.Any<string>());
    }

    [Test]
    public async Task ShouldNotCallGetUserNameAsyncOnceIfUnauthenticated()
    {
        // Arrange
        var requestLogger = new LoggingBehaviour<CreateLibraryCommand>(_logger, _user, _identityService);

        // Act
        await requestLogger.Process(new CreateLibraryCommand
        {
            MediaType = LibraryMediaType.Music,
            Title = "title",
            RootPath = "path"
        }, new CancellationToken());

        // Assert
        await _identityService.DidNotReceive().GetUserNameAsync(Arg.Any<string>());
    }
}
