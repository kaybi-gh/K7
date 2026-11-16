using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Commands.UpdateMediaShare;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Entities.Shares;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.MediaShares;

[TestFixture]
public class UpdateMediaShareCommandHandlerTests
{
    private MediaShareTestDatabase _database = null!;
    private IShareAccessInvalidator _invalidator = null!;
    private UpdateMediaShareCommandHandler _handler = null!;
    private MediaShare _share = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new MediaShareTestDatabase();
        _share = _database.AddShare(_database.MovieId, "link-token", s =>
        {
            s.PasswordHash = PinHashHelper.Hash("old-secret");
            s.FailedPasswordAttempts = 3;
            s.PasswordLockedUntil = DateTimeOffset.UtcNow.AddMinutes(10);
        });
        _database.Context.MediaShareSessions.Add(new MediaShareSession
        {
            Id = Guid.NewGuid(),
            ShareId = _share.Id,
            TokenHash = ShareTokenHelper.Hash("visitor-session"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
        _database.Context.SaveChanges();
        _database.Context.ChangeTracker.Clear();

        var actor = new ShareActor(_database.CreatorId, IsAdmin: false);
        var shareManager = Substitute.For<IMediaShareManager>();
        shareManager.RequireSharingActorAsync(Arg.Any<CancellationToken>()).Returns(actor);
        shareManager.GetManagedShareAsync(_share.Id, actor, Arg.Any<CancellationToken>())
            .Returns(_ => _database.Context.MediaShares.SingleAsync(s => s.Id == _share.Id));

        _invalidator = new ShareAccessInvalidator();
        _handler = new UpdateMediaShareCommandHandler(_database.Context, shareManager, _invalidator);
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    private UpdateMediaShareCommand Command(string? password) => new()
    {
        Id = _share.Id,
        Password = password,
        AllowPlayback = true
    };

    [Test]
    public async Task Handle_ShouldDropVisitorSessionsAndTheLock_WhenPasswordIsReplaced()
    {
        await _handler.Handle(Command("new-secret"), CancellationToken.None);

        (await _database.Context.MediaShareSessions.CountAsync()).Should().Be(0);
        var stored = await _database.Context.MediaShares.AsNoTracking().SingleAsync(s => s.Id == _share.Id);
        PinHashHelper.Verify(stored.PasswordHash!, "new-secret").Should().BeTrue();
        stored.FailedPasswordAttempts.Should().Be(0);
        stored.PasswordLockedUntil.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldKeepPasswordAndSessions_WhenPasswordIsNotSent()
    {
        await _handler.Handle(Command(password: null), CancellationToken.None);

        (await _database.Context.MediaShareSessions.CountAsync()).Should().Be(1);
        var stored = await _database.Context.MediaShares.AsNoTracking().SingleAsync(s => s.Id == _share.Id);
        PinHashHelper.Verify(stored.PasswordHash!, "old-secret").Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldRemoveThePassword_WhenAnEmptyOneIsSent()
    {
        await _handler.Handle(Command(""), CancellationToken.None);

        var stored = await _database.Context.MediaShares.AsNoTracking().SingleAsync(s => s.Id == _share.Id);
        stored.PasswordHash.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldInvalidateCachedVisitorAccess_WhenShareIsEdited()
    {
        var before = _invalidator.Version;

        await _handler.Handle(Command(password: null), CancellationToken.None);

        _invalidator.Version.Should().BeGreaterThan(before);
    }
}
