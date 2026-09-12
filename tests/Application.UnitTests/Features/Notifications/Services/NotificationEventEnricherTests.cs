using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.Notifications.Services;

[TestFixture]
public class NotificationEventEnricherTests
{
    [Test]
    public void EnrichWithGlobals_ShouldAddServerAndCurrentFields()
    {
        var server = Substitute.For<INotificationServerInfo>();
        server.Name.Returns("K7 Home");
        server.Url.Returns("https://k7.example");
        server.Version.Returns("1.2.3");
        var context = Substitute.For<IApplicationDbContext>();
        var enricher = new NotificationEventEnricher(context, server);

        var data = enricher.EnrichWithGlobals(new Dictionary<string, object?> { ["Custom"] = "x" });

        data["Custom"].Should().Be("x");
        data["Server.Name"].Should().Be("K7 Home");
        data["Server.Url"].Should().Be("https://k7.example");
        data["Server.Version"].Should().Be("1.2.3");
        data.Should().ContainKey("Current.Timestamp");
        data.Should().ContainKey("Current.UnixTime");
    }

    [Test]
    public async Task EnrichAsync_ShouldApplyUserCreatedAliases()
    {
        var server = Substitute.For<INotificationServerInfo>();
        server.Name.Returns("K7");
        server.Url.Returns("https://k7.example");
        server.Version.Returns("1.0.0");
        var context = Substitute.For<IApplicationDbContext>();
        var enricher = new NotificationEventEnricher(context, server);
        var userId = Guid.NewGuid();
        var domainEvent = new UserCreatedEvent(userId, "alice", "a@b.c", "User", UserCreationOrigin.Admin);

        var data = await enricher.EnrichAsync(domainEvent, new Dictionary<string, object?>());

        data["User.Name"].Should().Be("alice");
        data["User.Id"].Should().Be(userId.ToString());
        data["User.Email"].Should().Be("a@b.c");
        data["User.Role"].Should().Be("User");
        data["User.Origin"].Should().Be("Admin");
        data["Origin"].Should().Be("Admin");
    }

    [Test]
    public async Task EnrichAsync_ShouldApplyUserDeletedAliases_WithoutOrigin()
    {
        var server = Substitute.For<INotificationServerInfo>();
        server.Name.Returns("K7");
        server.Url.Returns("https://k7.example");
        server.Version.Returns("1.0.0");
        var context = Substitute.For<IApplicationDbContext>();
        var enricher = new NotificationEventEnricher(context, server);
        var userId = Guid.NewGuid();
        var domainEvent = new UserDeletedEvent(userId, "bob", null, "Administrator");

        var data = await enricher.EnrichAsync(domainEvent, new Dictionary<string, object?>());

        data["User.Name"].Should().Be("bob");
        data["User.Role"].Should().Be("Administrator");
        data.Should().NotContainKey("User.Origin");
    }
}
