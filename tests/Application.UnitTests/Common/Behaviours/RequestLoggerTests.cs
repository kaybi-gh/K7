using K7.Server.Application.Common.Behaviours;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Common.Behaviours;

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
        _user.IdentityId.Returns(Guid.NewGuid().ToString());
        var requestLogger = new LoggingBehaviour<CreateLibraryCommand>(_logger, _user, _identityService);

        // Act
        await requestLogger.Process(new CreateLibraryCommand
        {
            MediaType = LibraryMediaType.Music,
            Title = "title",
            RootPath = "path",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
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
            RootPath = "path",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        }, new CancellationToken());

        // Assert
        await _identityService.DidNotReceive().GetUserNameAsync(Arg.Any<string>());
    }
}
