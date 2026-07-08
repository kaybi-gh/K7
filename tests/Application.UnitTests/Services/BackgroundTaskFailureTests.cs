using K7.Server.Application.Services;
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
