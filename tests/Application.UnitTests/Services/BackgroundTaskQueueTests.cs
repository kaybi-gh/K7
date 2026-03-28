using K7.Server.Application.Services;

namespace K7.Server.Application.UnitTests.Services;

public class BackgroundTaskQueueTests
{
    private BackgroundTaskQueue _queue;

    [SetUp]
    public void Setup()
    {
        _queue = new BackgroundTaskQueue();
    }

    [Test]
    public async Task DequeueAsync_ShouldReturnEnqueuedId()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        // Act
        _queue.Enqueue(taskId);
        var result = await _queue.DequeueAsync(CancellationToken.None);

        // Assert
        result.Should().Be(taskId);
    }

    [Test]
    public async Task DequeueAsync_ShouldReturnItemsInFifoOrder()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        // Act
        _queue.Enqueue(id1);
        _queue.Enqueue(id2);
        _queue.Enqueue(id3);

        var result1 = await _queue.DequeueAsync(CancellationToken.None);
        var result2 = await _queue.DequeueAsync(CancellationToken.None);
        var result3 = await _queue.DequeueAsync(CancellationToken.None);

        // Assert
        result1.Should().Be(id1);
        result2.Should().Be(id2);
        result3.Should().Be(id3);
    }

    [Test]
    public async Task DequeueAsync_ShouldBlockUntilItemEnqueued()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var dequeueTask = _queue.DequeueAsync(CancellationToken.None);

        // Act
        await Task.Delay(50);
        dequeueTask.IsCompleted.Should().BeFalse();

        _queue.Enqueue(taskId);
        var result = await dequeueTask;

        // Assert
        result.Should().Be(taskId);
    }

    [Test]
    public void DequeueAsync_ShouldThrowWhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var dequeueTask = _queue.DequeueAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        var act = async () => await dequeueTask;
        act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public void Enqueue_ShouldAcceptMultipleItems()
    {
        // Arrange & Act
        for (var i = 0; i < 100; i++)
        {
            _queue.Enqueue(Guid.NewGuid());
        }

        // Assert — no exception thrown, channel is unbounded
    }
}
