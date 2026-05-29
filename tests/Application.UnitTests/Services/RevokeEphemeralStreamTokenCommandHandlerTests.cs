using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.StreamSessions.Commands.RevokeEphemeralStreamToken;
using K7.Server.Domain.Entities;
using MockQueryable.NSubstitute;

namespace K7.Server.Application.UnitTests.Services;

public class RevokeEphemeralStreamTokenCommandHandlerTests
{
    private IApplicationDbContext _context;
    private IUser _user;
    private RevokeEphemeralStreamTokenCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private List<EphemeralStreamToken> _tokens;

    [SetUp]
    public void Setup()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _user = Substitute.For<IUser>();
        _user.Id.Returns(_userId);
        _tokens = [];

        var dbSet = _tokens.BuildMockDbSet();
        _context.EphemeralStreamTokens.Returns(dbSet);

        _handler = new RevokeEphemeralStreamTokenCommandHandler(_context, _user);
    }

    [Test]
    public async Task Handle_ShouldRevokeAllActiveTokensForSession()
    {
        // Arrange
        _tokens.AddRange([
            new EphemeralStreamToken
            {
                Id = Guid.NewGuid(),
                Token = "token1",
                StreamSessionId = _sessionId,
                UserId = _userId,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
                IsRevoked = false,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new EphemeralStreamToken
            {
                Id = Guid.NewGuid(),
                Token = "token2",
                StreamSessionId = _sessionId,
                UserId = _userId,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
                IsRevoked = false,
                CreatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var dbSet = _tokens.BuildMockDbSet();
        _context.EphemeralStreamTokens.Returns(dbSet);

        var command = new RevokeEphemeralStreamTokenCommand { StreamSessionId = _sessionId };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _tokens.Should().AllSatisfy(t => t.IsRevoked.Should().BeTrue());
        await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldNotRevokeTokensOfOtherUsers()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        _tokens.AddRange([
            new EphemeralStreamToken
            {
                Id = Guid.NewGuid(),
                Token = "my-token",
                StreamSessionId = _sessionId,
                UserId = _userId,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
                IsRevoked = false,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new EphemeralStreamToken
            {
                Id = Guid.NewGuid(),
                Token = "other-token",
                StreamSessionId = _sessionId,
                UserId = otherUserId,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
                IsRevoked = false,
                CreatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var dbSet = _tokens.BuildMockDbSet();
        _context.EphemeralStreamTokens.Returns(dbSet);

        var command = new RevokeEphemeralStreamTokenCommand { StreamSessionId = _sessionId };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _tokens.First(t => t.Token == "my-token").IsRevoked.Should().BeTrue();
        _tokens.First(t => t.Token == "other-token").IsRevoked.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldNotRevokeAlreadyRevokedTokens()
    {
        // Arrange
        _tokens.Add(new EphemeralStreamToken
        {
            Id = Guid.NewGuid(),
            Token = "revoked-token",
            StreamSessionId = _sessionId,
            UserId = _userId,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
            IsRevoked = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var dbSet = _tokens.BuildMockDbSet();
        _context.EphemeralStreamTokens.Returns(dbSet);

        var command = new RevokeEphemeralStreamTokenCommand { StreamSessionId = _sessionId };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldNotRevokeTokensForOtherSessions()
    {
        // Arrange
        var otherSessionId = Guid.NewGuid();
        _tokens.Add(new EphemeralStreamToken
        {
            Id = Guid.NewGuid(),
            Token = "other-session-token",
            StreamSessionId = otherSessionId,
            UserId = _userId,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var dbSet = _tokens.BuildMockDbSet();
        _context.EphemeralStreamTokens.Returns(dbSet);

        var command = new RevokeEphemeralStreamTokenCommand { StreamSessionId = _sessionId };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _tokens.First().IsRevoked.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldHandleNoMatchingTokensGracefully()
    {
        // Arrange
        var command = new RevokeEphemeralStreamTokenCommand { StreamSessionId = _sessionId };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
