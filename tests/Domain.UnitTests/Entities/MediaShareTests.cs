using K7.Server.Domain.Entities.Shares;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.UnitTests.Entities;

[TestFixture]
public class MediaShareTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private static MediaShare CreateShare() => new()
    {
        Id = Guid.NewGuid(),
        RootMediaId = Guid.NewGuid(),
        TokenHash = "hash",
        CreatedByUserId = Guid.NewGuid()
    };

    [Test]
    public void GetStatus_ShouldReturnActive_WhenNothingLimitsTheShare()
    {
        var share = CreateShare();

        share.GetStatus(Now).Should().Be(MediaShareStatus.Active);
        share.AcceptsNewSessions(Now).Should().BeTrue();
    }

    [Test]
    public void GetStatus_ShouldReturnExpired_WhenExpirationIsReached()
    {
        var share = CreateShare();
        share.ExpiresAt = Now;

        share.GetStatus(Now).Should().Be(MediaShareStatus.Expired);
        share.AcceptsNewSessions(Now).Should().BeFalse();
    }

    [Test]
    public void GetStatus_ShouldReturnExhausted_WhenAccessLimitIsReached()
    {
        var share = CreateShare();
        share.MaxAccessCount = 2;
        share.AccessCount = 2;

        share.GetStatus(Now).Should().Be(MediaShareStatus.Exhausted);
    }

    [Test]
    public void GetStatus_ShouldReturnRevoked_WhenRevokedEvenIfAlsoExpired()
    {
        var share = CreateShare();
        share.ExpiresAt = Now.AddDays(-1);
        share.RevokedAt = Now.AddHours(-1);

        share.GetStatus(Now).Should().Be(MediaShareStatus.Revoked);
    }

    [Test]
    public void GetStatus_ShouldReturnConsumed_WhenConsumed()
    {
        var share = CreateShare();
        share.CloseWhenConsumed = true;
        share.ConsumedAt = Now.AddMinutes(-1);

        share.GetStatus(Now).Should().Be(MediaShareStatus.Consumed);
        share.AcceptsNewSessions(Now).Should().BeFalse();
    }

    [Test]
    public void AllowsExistingSession_ShouldReturnTrue_WhenAccessLimitIsReached()
    {
        var share = CreateShare();
        share.MaxAccessCount = 1;
        share.AccessCount = 1;

        share.AllowsExistingSession(Now.AddMinutes(-10), Now).Should().BeTrue();
    }

    [Test]
    public void AllowsExistingSession_ShouldReturnFalse_WhenRevoked()
    {
        var share = CreateShare();
        share.RevokedAt = Now.AddMinutes(-1);

        share.AllowsExistingSession(Now.AddMinutes(-10), Now).Should().BeFalse();
    }

    [Test]
    public void AllowsExistingSession_ShouldReturnFalse_WhenExpired()
    {
        var share = CreateShare();
        share.ExpiresAt = Now.AddSeconds(-1);

        share.AllowsExistingSession(Now.AddMinutes(-10), Now).Should().BeFalse();
    }

    [Test]
    public void AllowsExistingSession_ShouldReturnTrue_WhenConsumedWithinGracePeriod()
    {
        var share = CreateShare();
        share.ConsumedAt = Now.AddMinutes(-5);

        share.AllowsExistingSession(Now.AddHours(-1), Now).Should().BeTrue();
    }

    [Test]
    public void AllowsExistingSession_ShouldReturnFalse_WhenConsumedGracePeriodIsOver()
    {
        var share = CreateShare();
        share.ConsumedAt = Now - MediaShare.ConsumedGracePeriod - TimeSpan.FromSeconds(1);

        share.AllowsExistingSession(Now.AddHours(-2), Now).Should().BeFalse();
    }

    [Test]
    public void AllowsExistingSession_ShouldReturnFalse_WhenSessionStartedAfterConsumption()
    {
        var share = CreateShare();
        share.ConsumedAt = Now.AddMinutes(-5);

        share.AllowsExistingSession(Now.AddMinutes(-1), Now).Should().BeFalse();
    }
}
