using Ardalis.GuardClauses;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.StreamSessions.Commands.GenerateEphemeralStreamToken;
using K7.Server.Domain.Entities;
using MockQueryable.NSubstitute;

namespace K7.Server.Application.UnitTests.Services;

public class GenerateEphemeralStreamTokenCommandHandlerTests
{
    private IApplicationDbContext _context;
    private IUser _user;
    private GenerateEphemeralStreamTokenCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _user = Substitute.For<IUser>();
        _user.Id.Returns(_userId);

        var session = new StreamSession
        {
            Id = _sessionId,
            UserId = _userId,
            IndexedFileId = Guid.NewGuid()
        };
        _context.StreamSessions.FindAsync(Arg.Is<object[]>(x => (Guid)x[0] == _sessionId), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<StreamSession?>(session));

        var tokens = new List<EphemeralStreamToken>();
        var dbSet = tokens.BuildMockDbSet();
        _context.EphemeralStreamTokens.Returns(dbSet);

        _handler = new GenerateEphemeralStreamTokenCommandHandler(_context, _user);
    }

    [Test]
    public async Task Handle_ShouldReturnToken_WhenSessionBelongsToUser()
    {
        // Arrange
        var command = new GenerateEphemeralStreamTokenCommand { StreamSessionId = _sessionId };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Handle_ShouldAddTokenToDbSet()
    {
        // Arrange
        var command = new GenerateEphemeralStreamTokenCommand { StreamSessionId = _sessionId };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _context.EphemeralStreamTokens.Received(1).Add(Arg.Is<EphemeralStreamToken>(t =>
            t.StreamSessionId == _sessionId
            && t.UserId == _userId
            && !t.IsRevoked
            && t.Token.Length > 0
        ));
        await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldSetExpiryToEightHours()
    {
        // Arrange
        var command = new GenerateEphemeralStreamTokenCommand { StreamSessionId = _sessionId };
        var before = DateTimeOffset.UtcNow.AddHours(8);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _context.EphemeralStreamTokens.Received(1).Add(Arg.Is<EphemeralStreamToken>(t =>
            t.ExpiresAt >= before.AddSeconds(-5) && t.ExpiresAt <= before.AddSeconds(5)
        ));
    }

    [Test]
    public async Task Handle_ShouldThrowForbiddenAccess_WhenSessionBelongsToAnotherUser()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var session = new StreamSession
        {
            Id = _sessionId,
            UserId = otherUserId,
            IndexedFileId = Guid.NewGuid()
        };
        _context.StreamSessions.FindAsync(Arg.Any<object[]>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<StreamSession?>(session));

        var command = new GenerateEphemeralStreamTokenCommand { StreamSessionId = _sessionId };

        // Act
        var action = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task Handle_ShouldThrowNotFoundException_WhenSessionDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        _context.StreamSessions.FindAsync(Arg.Is<object[]>(x => (Guid)x[0] == nonExistentId), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<StreamSession?>((StreamSession?)null));

        var command = new GenerateEphemeralStreamTokenCommand { StreamSessionId = nonExistentId };

        // Act
        var action = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ShouldGenerateUniqueTokens()
    {
        // Arrange
        var command = new GenerateEphemeralStreamTokenCommand { StreamSessionId = _sessionId };

        // Act
        var token1 = await _handler.Handle(command, CancellationToken.None);
        var token2 = await _handler.Handle(command, CancellationToken.None);

        // Assert
        token1.Should().NotBe(token2);
    }
}
