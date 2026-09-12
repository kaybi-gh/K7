using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.Notifications.Services;

[TestFixture]
public class OutboundNotificationDispatcherTests
{
    [Test]
    public async Task DispatchAsync_ShouldNotSend_WhenNoMatchingRules()
    {
        var provider = Substitute.For<INotificationProvider>();
        var (dispatcher, _) = CreateDispatcher(
            [
                Rule("other", nameof(Domain.Events.UserCreatedEvent), enabled: true)
            ],
            provider);

        await dispatcher.DispatchAsync(
            nameof(Domain.Events.UserDeletedEvent),
            new Dictionary<string, object?>(),
            null,
            CancellationToken.None);

        await provider.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Test]
    public async Task DispatchAsync_ShouldSkip_WhenOutsideScheduleWindow()
    {
        var provider = Substitute.For<INotificationProvider>();
        var tomorrow = DateTime.Today.AddDays(1).DayOfWeek;
        var (dispatcher, _) = CreateDispatcher(
            [
                Rule(
                    "scheduled",
                    nameof(Domain.Events.UserCreatedEvent),
                    enabled: true,
                    windows:
                    [
                        new NotificationScheduleWindow
                        {
                            Days = [tomorrow],
                            Start = new TimeOnly(0, 0),
                            End = new TimeOnly(23, 59)
                        }
                    ])
            ],
            provider);

        await dispatcher.DispatchAsync(
            nameof(Domain.Events.UserCreatedEvent),
            new Dictionary<string, object?>(),
            null,
            CancellationToken.None);

        await provider.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Test]
    public async Task DispatchAsync_ShouldSkip_WhenInCooldown()
    {
        var provider = Substitute.For<INotificationProvider>();
        var (dispatcher, _) = CreateDispatcher(
            [
                Rule(
                    "cooldown",
                    nameof(Domain.Events.UserCreatedEvent),
                    enabled: true,
                    cooldownSeconds: 3600,
                    lastSentAt: DateTimeOffset.UtcNow)
            ],
            provider);

        await dispatcher.DispatchAsync(
            nameof(Domain.Events.UserCreatedEvent),
            new Dictionary<string, object?>(),
            null,
            CancellationToken.None);

        await provider.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Test]
    public async Task DispatchAsync_ShouldSend_WhenRuleMatches()
    {
        var provider = Substitute.For<INotificationProvider>();
        provider.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var (dispatcher, _) = CreateDispatcher(
            [
                Rule("match", nameof(Domain.Events.UserCreatedEvent), enabled: true)
            ],
            provider);

        await dispatcher.DispatchAsync(
            nameof(Domain.Events.UserCreatedEvent),
            new Dictionary<string, object?> { ["User.Name"] = "alice" },
            null,
            CancellationToken.None);

        await provider.Received(1).SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private static (OutboundNotificationDispatcher Dispatcher, IApplicationDbContext Context) CreateDispatcher(
        List<NotificationRule> rules,
        INotificationProvider provider)
    {
        var context = Substitute.For<IApplicationDbContext>();
        var dbSet = rules.BuildMockDbSet();
        context.NotificationRules.Returns(dbSet);

        var server = Substitute.For<INotificationServerInfo>();
        server.Name.Returns("K7");
        server.Url.Returns("https://k7.example");
        server.Version.Returns("1.0.0");

        var services = new ServiceCollection();
        services.AddKeyedSingleton(NotificationProviderType.Webhook, provider);
        var sp = services.BuildServiceProvider();

        var dispatcher = new OutboundNotificationDispatcher(
            context,
            sp,
            new NotificationConditionEvaluator(),
            new NotificationPayloadRenderer(),
            new NotificationEventEnricher(context, server),
            NullLogger<OutboundNotificationDispatcher>.Instance);

        return (dispatcher, context);
    }

    private static NotificationRule Rule(
        string name,
        string eventType,
        bool enabled,
        int? cooldownSeconds = null,
        DateTimeOffset? lastSentAt = null,
        List<NotificationScheduleWindow>? windows = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsEnabled = enabled,
            ProviderType = NotificationProviderType.Webhook,
            PayloadFormat = NotificationPayloadFormat.Structured,
            EventTypeNames = [eventType],
            ProviderConfig = """{"url":"https://example/hook"}""",
            TitleTemplate = "t",
            BodyTemplate = "b",
            ScheduleWindows = windows ?? [],
            CooldownSeconds = cooldownSeconds,
            LastSentAt = lastSentAt
        };
}
