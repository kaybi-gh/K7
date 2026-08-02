using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class BackgroundTaskFailureTests
{
    [Test]
    public void MarkFailed_ShouldClearRetryState()
    {
        var task = CreateInProgressTask();

        BackgroundTaskFailure.MarkFailed(task);

        task.Status.Should().Be(BackgroundTaskStatus.Failed);
        task.CompletedAt.Should().NotBeNull();
        task.NextRetryAfter.Should().BeNull();
        task.StartedAt.Should().BeNull();
    }

    [Test]
    public void MarkCancelled_ShouldClearRetryState()
    {
        var task = CreateInProgressTask();

        BackgroundTaskFailure.MarkCancelled(task);

        task.Status.Should().Be(BackgroundTaskStatus.Cancelled);
        task.CompletedAt.Should().NotBeNull();
        task.NextRetryAfter.Should().BeNull();
        task.StartedAt.Should().BeNull();
    }

    [Test]
    public void HandleFailure_ShouldScheduleRetry_WhenAttemptsRemain()
    {
        var task = CreateInProgressTask();

        BackgroundTaskFailure.Handle(task, new InvalidOperationException("Transient failure"), TimeSpan.FromMinutes(15));

        task.Status.Should().Be(BackgroundTaskStatus.WaitingForRetry);
        task.NextRetryAfter.Should().NotBeNull();
        task.CompletedAt.Should().BeNull();
    }

    [Test]
    public void ScheduleRateLimitedRetry_ShouldAlignNextRetryAndBoostPriority()
    {
        var task = CreateInProgressTask();
        task.Priority = 0;
        var now = DateTimeOffset.Parse("2026-08-02T10:00:00Z");
        var retryAfter = TimeSpan.FromSeconds(90);

        BackgroundTaskFailure.ScheduleRateLimitedRetry(task, retryAfter, now);

        task.Status.Should().Be(BackgroundTaskStatus.WaitingForRetry);
        task.NextRetryAfter.Should().Be(now.Add(retryAfter));
        task.Priority.Should().Be(BackgroundTaskScheduling.OnDemandBoost);
    }

    [Test]
    public void Handle_ShouldUseProviderRateLimitedPath()
    {
        var task = CreateInProgressTask();
        var before = DateTimeOffset.UtcNow;

        BackgroundTaskFailure.Handle(
            task,
            new ProviderRateLimitedException("tvdb", TimeSpan.FromSeconds(30)),
            TimeSpan.FromMinutes(15));

        task.Status.Should().Be(BackgroundTaskStatus.WaitingForRetry);
        task.NextRetryAfter.Should().BeOnOrAfter(before.AddSeconds(29));
        task.NextRetryAfter.Should().BeOnOrBefore(DateTimeOffset.UtcNow.AddSeconds(35));
        task.Priority.Should().BeGreaterThanOrEqualTo(BackgroundTaskScheduling.OnDemandBoost);
    }

    [Test]
    public void ExecutionContext_Cancel_ShouldExposeCancellationDetails()
    {
        var context = new BackgroundTaskExecutionContext();

        context.Cancel("Remote metadata picture unavailable (403)");

        context.IsCancelled.Should().BeTrue();
        context.CancellationDetails.Should().Be("Remote metadata picture unavailable (403)");
    }

    private static BackgroundTask CreateInProgressTask() => new()
    {
        Id = Guid.NewGuid(),
        Name = "TestTask",
        RequestType = "Test",
        RequestData = "{}",
        Status = BackgroundTaskStatus.InProgress,
        StartedAt = DateTimeOffset.UtcNow,
        NextRetryAfter = DateTimeOffset.UtcNow.AddMinutes(1),
        AttemptCount = 0,
        MaxAttempts = 5
    };
}
