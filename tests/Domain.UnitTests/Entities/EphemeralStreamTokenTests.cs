using K7.Server.Domain.Entities;

namespace K7.Server.Domain.UnitTests.Entities;

[TestFixture]
public class EphemeralStreamTokenTests
{
    [Test]
    public void IsUsable_ShouldReturnTrue_WhenNotRevokedAndNotExpired()
    {
        var now = DateTimeOffset.Parse("2026-07-15T12:00:00Z");
        var token = new EphemeralStreamToken
        {
            Token = "abc",
            StreamSessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ExpiresAt = now.AddMinutes(5),
            CreatedAt = now
        };

        token.IsUsable(now).Should().BeTrue();
    }

    [Test]
    public void IsUsable_ShouldReturnFalse_WhenRevoked()
    {
        var now = DateTimeOffset.Parse("2026-07-15T12:00:00Z");
        var token = new EphemeralStreamToken
        {
            Token = "abc",
            StreamSessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ExpiresAt = now.AddMinutes(5),
            IsRevoked = true,
            CreatedAt = now
        };

        token.IsUsable(now).Should().BeFalse();
    }

    [Test]
    public void IsUsable_ShouldReturnFalse_WhenExpired()
    {
        var now = DateTimeOffset.Parse("2026-07-15T12:00:00Z");
        var token = new EphemeralStreamToken
        {
            Token = "abc",
            StreamSessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ExpiresAt = now.AddSeconds(-1),
            CreatedAt = now.AddMinutes(-10)
        };

        token.IsUsable(now).Should().BeFalse();
    }

    [Test]
    public void IsUsable_ShouldReturnTrue_WhenExpiresExactlyAtNow()
    {
        var now = DateTimeOffset.Parse("2026-07-15T12:00:00Z");
        var token = new EphemeralStreamToken
        {
            Token = "abc",
            StreamSessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ExpiresAt = now,
            CreatedAt = now.AddMinutes(-10)
        };

        token.IsUsable(now).Should().BeTrue();
    }
}
