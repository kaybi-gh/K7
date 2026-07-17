using K7.Server.Application.Features.Notifications.Services.Descriptors;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.UnitTests.Features.Notifications.Services.Descriptors;

[TestFixture]
public class NotificationEventDescriptorTests
{
    [Test]
    public void LibraryScanCompleted_ShouldMatchEventTypeName_AndBeLibraryCategory()
    {
        var descriptor = new LibraryScanCompletedEventDescriptor();

        descriptor.EventTypeName.Should().Be(nameof(LibraryScanCompletedEvent));
        descriptor.Category.Should().Be(NotificationEventCategory.Library);
        descriptor.Parameters.Should().Contain(p => p.Name == "AddedCount");
    }

    [Test]
    public void PeerConnectivityChanged_ShouldMatchEventTypeName_AndBeFederationCategory()
    {
        var descriptor = new PeerConnectivityChangedEventDescriptor();

        descriptor.EventTypeName.Should().Be(nameof(PeerConnectivityChangedEvent));
        descriptor.Category.Should().Be(NotificationEventCategory.Federation);
        descriptor.Parameters.Should().Contain(p => p.Name == "Succeeded");
    }

    [Test]
    public void TranscodeFailed_ShouldMatchEventTypeName_AndBeHealthCategory()
    {
        var descriptor = new TranscodeFailedEventDescriptor();

        descriptor.EventTypeName.Should().Be(nameof(TranscodeFailedEvent));
        descriptor.Category.Should().Be(NotificationEventCategory.Health);
        descriptor.Parameters.Should().Contain(p => p.Name == "ErrorMessage");
    }

    [Test]
    public void MusicIntelligenceUnavailable_ShouldMatchEventTypeName_AndBeHealthCategory()
    {
        var descriptor = new MusicIntelligenceUnavailableEventDescriptor();

        descriptor.EventTypeName.Should().Be(nameof(MusicIntelligenceUnavailableEvent));
        descriptor.Category.Should().Be(NotificationEventCategory.Health);
    }

    [Test]
    public void MediaCreated_ShouldMatchEventTypeName_AndBeMediaCategory()
    {
        var descriptor = new MediaCreatedEventDescriptor();

        descriptor.EventTypeName.Should().Be(nameof(MediaCreatedEvent));
        descriptor.Category.Should().Be(NotificationEventCategory.Media);
    }
}
