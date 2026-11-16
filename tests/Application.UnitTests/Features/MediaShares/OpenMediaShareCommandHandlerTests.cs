using Ardalis.GuardClauses;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Commands.OpenMediaShare;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Shares;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.MediaShares;

[TestFixture]
public class OpenMediaShareCommandHandlerTests
{
    private const string Token = "share-link-token";

    private MediaShareTestDatabase _database = null!;
    private IMediaShareManager _shareManager = null!;
    private OpenMediaShareCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new MediaShareTestDatabase();
        _shareManager = Substitute.For<IMediaShareManager>();
        _shareManager.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(true);
        _shareManager.CanUserShareAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _handler = new OpenMediaShareCommandHandler(_database.Context, _shareManager);
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    private Task<OpenMediaShareResult> OpenAsync(string? password = null, params string[] existingSessionTokens) =>
        _handler.Handle(
            new OpenMediaShareCommand
            {
                Token = Token,
                Password = password,
                ExistingSessionTokens = existingSessionTokens
            },
            CancellationToken.None);

    [Test]
    public async Task Handle_ShouldOpenASessionAndCountOneAccess_WhenShareIsActive()
    {
        var share = _database.AddShare(_database.MovieId, Token, s => s.AllowDownload = true);

        var result = await OpenAsync();

        result.Dto.Outcome.Should().Be(OpenShareOutcome.Opened);
        result.Dto.ShareId.Should().Be(share.Id);
        result.Dto.RootMediaId.Should().Be(_database.MovieId);
        result.Dto.RootMediaType.Should().Be(MediaType.Movie);
        result.Dto.AllowDownload.Should().BeTrue();
        result.SessionToken.Should().NotBeNullOrEmpty();
        result.IsNewSession.Should().BeTrue();

        var stored = await _database.Context.MediaShares.AsNoTracking().SingleAsync(s => s.Id == share.Id);
        stored.AccessCount.Should().Be(1);
        stored.LastAccessedAt.Should().NotBeNull();

        var session = await _database.Context.MediaShareSessions.AsNoTracking().SingleAsync();
        session.TokenHash.Should().Be(ShareTokenHelper.Hash(result.SessionToken!));
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenTokenIsUnknown()
    {
        _database.AddShare(_database.MovieId, "another-token");

        var act = () => OpenAsync();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenPublicSharesAreDisabled()
    {
        _database.AddShare(_database.MovieId, Token);
        _shareManager.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(false);

        var act = () => OpenAsync();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ShouldAskForPassword_WhenShareIsProtectedAndNoneIsGiven()
    {
        _database.AddShare(_database.MovieId, Token, s => s.PasswordHash = PinHashHelper.Hash("secret"));

        var result = await OpenAsync();

        result.Dto.Outcome.Should().Be(OpenShareOutcome.PasswordRequired);
        result.Dto.ShareId.Should().BeNull();
        result.SessionToken.Should().BeNull();
        (await _database.Context.MediaShareSessions.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_ShouldRefuseAndNotCount_WhenPasswordIsWrong()
    {
        var share = _database.AddShare(_database.MovieId, Token, s => s.PasswordHash = PinHashHelper.Hash("secret"));

        var result = await OpenAsync("wrong");

        result.Dto.Outcome.Should().Be(OpenShareOutcome.InvalidPassword);
        result.SessionToken.Should().BeNull();
        var stored = await _database.Context.MediaShares.AsNoTracking().SingleAsync(s => s.Id == share.Id);
        stored.AccessCount.Should().Be(0);
    }

    [Test]
    public async Task Handle_ShouldLockTheShare_WhenTooManyWrongPasswordsAreTried()
    {
        var share = _database.AddShare(_database.MovieId, Token, s => s.PasswordHash = PinHashHelper.Hash("secret"));

        OpenMediaShareResult? last = null;
        for (var attempt = 0; attempt < MediaShareRules.MaxPasswordAttempts; attempt++)
            last = await OpenAsync("wrong");

        last!.Dto.Outcome.Should().Be(OpenShareOutcome.PasswordLocked);
        var stored = await _database.Context.MediaShares.AsNoTracking().SingleAsync(s => s.Id == share.Id);
        stored.PasswordLockedUntil.Should().BeAfter(DateTimeOffset.UtcNow);

        // Even the right password is refused while the share is locked.
        var afterLock = await OpenAsync("secret");
        afterLock.Dto.Outcome.Should().Be(OpenShareOutcome.PasswordLocked);
        afterLock.SessionToken.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldAcceptThePasswordAgain_WhenTheLockIsOver()
    {
        _database.AddShare(_database.MovieId, Token, s =>
        {
            s.PasswordHash = PinHashHelper.Hash("secret");
            s.PasswordLockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
        });

        var result = await OpenAsync("secret");

        result.Dto.Outcome.Should().Be(OpenShareOutcome.Opened);
    }

    [Test]
    public async Task Handle_ShouldForgetWrongAttempts_WhenTheRightPasswordIsGiven()
    {
        var share = _database.AddShare(_database.MovieId, Token, s => s.PasswordHash = PinHashHelper.Hash("secret"));
        await OpenAsync("wrong");
        await OpenAsync("wrong");

        var result = await OpenAsync("secret");

        result.Dto.Outcome.Should().Be(OpenShareOutcome.Opened);
        var stored = await _database.Context.MediaShares.AsNoTracking().SingleAsync(s => s.Id == share.Id);
        stored.FailedPasswordAttempts.Should().Be(0);
    }

    [Test]
    public async Task Handle_ShouldOpen_WhenPasswordIsRight()
    {
        _database.AddShare(_database.MovieId, Token, s => s.PasswordHash = PinHashHelper.Hash("secret"));

        var result = await OpenAsync("secret");

        result.Dto.Outcome.Should().Be(OpenShareOutcome.Opened);
    }

    [Test]
    public async Task Handle_ShouldRefuseNewVisitors_WhenAccessLimitIsReached()
    {
        _database.AddShare(_database.MovieId, Token, s => s.MaxAccessCount = 1);

        var first = await OpenAsync();
        var second = await OpenAsync();

        first.Dto.Outcome.Should().Be(OpenShareOutcome.Opened);
        second.Dto.Outcome.Should().Be(OpenShareOutcome.Unavailable);
        second.Dto.Status.Should().Be(MediaShareStatus.Exhausted);
        second.SessionToken.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldReuseTheSessionWithoutCounting_WhenVisitorComesBack()
    {
        var share = _database.AddShare(_database.MovieId, Token, s =>
        {
            s.MaxAccessCount = 1;
            s.PasswordHash = PinHashHelper.Hash("secret");
        });
        var first = await OpenAsync("secret");

        // No password this time: the visitor still holds its session.
        var second = await OpenAsync(password: null, first.SessionToken!);

        second.Dto.Outcome.Should().Be(OpenShareOutcome.Opened);
        second.SessionToken.Should().Be(first.SessionToken);
        second.IsNewSession.Should().BeFalse();
        var stored = await _database.Context.MediaShares.AsNoTracking().SingleAsync(s => s.Id == share.Id);
        stored.AccessCount.Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldNotReuseASessionOfAnotherShare()
    {
        _database.AddShare(_database.MovieId, Token, s => s.PasswordHash = PinHashHelper.Hash("secret"));
        _database.AddShare(_database.SerieId, "open-token");
        var other = await _handler.Handle(
            new OpenMediaShareCommand { Token = "open-token" },
            CancellationToken.None);

        var result = await OpenAsync(password: null, other.SessionToken!);

        result.Dto.Outcome.Should().Be(OpenShareOutcome.PasswordRequired);
    }

    [Test]
    public async Task Handle_ShouldReportExpired_WhenShareIsExpired()
    {
        _database.AddShare(_database.MovieId, Token, s => s.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = await OpenAsync();

        result.Dto.Outcome.Should().Be(OpenShareOutcome.Unavailable);
        result.Dto.Status.Should().Be(MediaShareStatus.Expired);
    }

    [Test]
    public async Task Handle_ShouldReportRevoked_WhenShareIsRevoked()
    {
        _database.AddShare(_database.MovieId, Token, s => s.RevokedAt = DateTimeOffset.UtcNow);

        var result = await OpenAsync();

        result.Dto.Outcome.Should().Be(OpenShareOutcome.Unavailable);
        result.Dto.Status.Should().Be(MediaShareStatus.Revoked);
    }

    [Test]
    public async Task Handle_ShouldReportSuspended_WhenCreatorMayNoLongerShare()
    {
        _database.AddShare(_database.MovieId, Token);
        _shareManager.CanUserShareAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await OpenAsync();

        result.Dto.Outcome.Should().Be(OpenShareOutcome.Unavailable);
        result.Dto.Status.Should().Be(MediaShareStatus.Suspended);
    }

    [Test]
    public async Task Handle_ShouldNotOutliveTheShare_WhenShareExpiresBeforeTheSessionLifetime()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        _database.AddShare(_database.MovieId, Token, s => s.ExpiresAt = expiresAt);

        var result = await OpenAsync();

        result.Dto.SessionExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }
}
