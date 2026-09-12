using K7.Server.Application;
using K7.Server.Application.Features.Notifications.EventHandlers;
using K7.Server.Application.Features.Scrobbling.EventHandlers;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Events;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.UnitTests.Features.Notifications;

[TestFixture]
public class OutboundNotificationHandlerRegistrationTests
{
    [Test]
    public void AddApplicationServices_ShouldRegisterSingleOutboundHandler_PerUserEvent()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();

        var created = services
            .Where(d => d.ServiceType == typeof(INotificationHandler<UserCreatedEvent>))
            .ToList();
        var deleted = services
            .Where(d => d.ServiceType == typeof(INotificationHandler<UserDeletedEvent>))
            .ToList();
        var openGeneric = services
            .Where(d => d.ServiceType == typeof(INotificationHandler<>)
                        && d.ImplementationType == typeof(OutboundNotificationEventHandler<>))
            .ToList();

        created.Should().ContainSingle()
            .Which.ImplementationType.Should().Be(typeof(OutboundNotificationEventHandler<UserCreatedEvent>));
        deleted.Should().ContainSingle()
            .Which.ImplementationType.Should().Be(typeof(OutboundNotificationEventHandler<UserDeletedEvent>));
        openGeneric.Should().BeEmpty();
    }

    [Test]
    public void AddApplicationServices_ShouldNotDuplicateScrobbleHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();

        services.Where(d => d.ServiceType == typeof(INotificationHandler<PlaybackStateChangedEvent>)
                            && d.ImplementationType == typeof(ScrobblePlaybackStateChangedHandler))
            .Should().ContainSingle();

        services.Where(d => d.ServiceType == typeof(INotificationHandler<MediaPlaybackCompletedEvent<Movie>>)
                            && d.ImplementationType == typeof(ScrobblePlaybackCompletedHandler<Movie>))
            .Should().ContainSingle();

        services.Where(d => d.ServiceType == typeof(INotificationHandler<>)
                            && d.ImplementationType == typeof(ScrobblePlaybackCompletedHandler<>))
            .Should().BeEmpty();
    }
}
