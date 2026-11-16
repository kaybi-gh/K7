using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Entities.Shares;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.MediaShares;

[TestFixture]
public class ShareSessionValidatorTests
{
    private const string SessionToken = "visitor-session-token";

    private MediaShareTestDatabase _database = null!;
    private IMediaShareManager _shareManager = null!;
    private ShareSessionValidator _validator = null!;
    private MediaShare _share = null!;
    private Guid _sessionId;

    [SetUp]
    public void SetUp()
    {
        _database = new MediaShareTestDatabase();
        _shareManager = Substitute.For<IMediaShareManager>();
        _shareManager.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(true);
        _shareManager.CanUserShareAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _validator = new ShareSessionValidator(_database.Context, _shareManager);

        _share = _database.AddShare(_database.MovieId, "link-token");
        _sessionId = AddSession(_share.Id, SessionToken, DateTimeOffset.UtcNow.AddHours(1));
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    private Guid AddSession(Guid shareId, string token, DateTimeOffset expiresAt)
    {
        var session = new MediaShareSession
        {
            Id = Guid.NewGuid(),
            ShareId = shareId,
            TokenHash = ShareTokenHelper.Hash(token),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresAt = expiresAt
        };
        _database.Context.MediaShareSessions.Add(session);
        _database.Context.SaveChanges();
        _database.Context.ChangeTracker.Clear();
        return session.Id;
    }

    private async Task UpdateShareAsync(Action<MediaShare> change)
    {
        var share = await _database.Context.MediaShares.SingleAsync(s => s.Id == _share.Id);
        change(share);
        await _database.Context.SaveChangesAsync();
        _database.Context.ChangeTracker.Clear();
    }

    [Test]
    public async Task ValidateAsync_ShouldGrantTheShare_WhenSessionIsValid()
    {
        var grants = await _validator.ValidateAsync([SessionToken]);

        grants.Should().ContainSingle();
        grants[0].ShareId.Should().Be(_share.Id);
        grants[0].SessionId.Should().Be(_sessionId);
    }

    [Test]
    public async Task ValidateAsync_ShouldReturnNothing_WhenTokenIsUnknown()
    {
        var grants = await _validator.ValidateAsync(["not-a-session"]);

        grants.Should().BeEmpty();
    }

    [Test]
    public async Task ValidateAsync_ShouldReturnNothing_WhenPublicSharesAreDisabled()
    {
        _shareManager.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(false);

        var grants = await _validator.ValidateAsync([SessionToken]);

        grants.Should().BeEmpty();
    }

    [Test]
    public async Task ValidateAsync_ShouldReturnNothing_WhenSessionIsExpired()
    {
        AddSession(_share.Id, "old-session", DateTimeOffset.UtcNow.AddMinutes(-1));

        var grants = await _validator.ValidateAsync(["old-session"]);

        grants.Should().BeEmpty();
    }

    [Test]
    public async Task ValidateAsync_ShouldReturnNothing_WhenShareIsRevoked()
    {
        await UpdateShareAsync(share => share.RevokedAt = DateTimeOffset.UtcNow);

        var grants = await _validator.ValidateAsync([SessionToken]);

        grants.Should().BeEmpty();
    }

    [Test]
    public async Task ValidateAsync_ShouldKeepTheSession_WhenAccessLimitIsReached()
    {
        await UpdateShareAsync(share =>
        {
            share.MaxAccessCount = 1;
            share.AccessCount = 1;
        });

        var grants = await _validator.ValidateAsync([SessionToken]);

        grants.Should().ContainSingle();
    }

    [Test]
    public async Task ValidateAsync_ShouldReturnNothing_WhenCreatorMayNoLongerShare()
    {
        _shareManager.CanUserShareAsync(_database.CreatorId, Arg.Any<CancellationToken>()).Returns(false);

        var grants = await _validator.ValidateAsync([SessionToken]);

        grants.Should().BeEmpty();
    }
}
