using AwesomeAssertions;
using K7.Server.Application.Services;

namespace K7.Server.Application.UnitTests.Services;

/// <summary>
/// The lock is what prevents duplicate medias: creation is a check-then-insert and the database has no
/// unique constraint on media identity, so two concurrent commands for the same album would both insert.
/// </summary>
public class MediaIdentityLockTests
{
    [Test]
    public async Task AcquireAsync_ShouldSerializeSameKey()
    {
        var identityLock = new MediaIdentityLock();
        var concurrent = 0;
        var maxConcurrent = 0;
        var guard = new Lock();

        await Task.WhenAll(Enumerable.Range(0, 16).Select(async _ =>
        {
            await using var handle = await identityLock.AcquireAsync("album:abbey road");

            lock (guard)
            {
                concurrent++;
                maxConcurrent = Math.Max(maxConcurrent, concurrent);
            }

            await Task.Delay(5);

            lock (guard)
            {
                concurrent--;
            }
        }));

        maxConcurrent.Should().Be(1);
    }

    [Test]
    public async Task AcquireAsync_ShouldNotBlockDifferentKeys()
    {
        var identityLock = new MediaIdentityLock();

        await using var first = await identityLock.AcquireAsync("album:abbey road");

        // Would deadlock if distinct identities shared a lock.
        var second = await identityLock.AcquireAsync("album:revolver").WaitAsync(TimeSpan.FromSeconds(5));
        await second.DisposeAsync();
    }

    [Test]
    public async Task AcquireAsync_ShouldBeReentrantAcrossSequentialAcquisitions()
    {
        var identityLock = new MediaIdentityLock();

        for (var i = 0; i < 3; i++)
        {
            await using var handle = await identityLock.AcquireAsync("serie:the wire");
        }

        // Reacquiring after every holder released must still succeed, which also proves the entry was
        // recreated cleanly after being evicted.
        var handleAfterEviction = await identityLock.AcquireAsync("serie:the wire").WaitAsync(TimeSpan.FromSeconds(5));
        await handleAfterEviction.DisposeAsync();
    }

    [Test]
    public async Task DisposeAsync_ShouldBeIdempotent()
    {
        var identityLock = new MediaIdentityLock();
        var handle = await identityLock.AcquireAsync("movie:inception");

        await handle.DisposeAsync();
        await handle.DisposeAsync();

        // A double dispose must not have released the semaphore twice, which would let two holders in.
        await using var next = await identityLock.AcquireAsync("movie:inception");
        var second = identityLock.AcquireAsync("movie:inception");
        second.IsCompleted.Should().BeFalse();

        await next.DisposeAsync();
        await (await second).DisposeAsync();
    }

    [Test]
    public void AcquireAsync_ShouldRejectEmptyKey()
    {
        var identityLock = new MediaIdentityLock();

        var act = async () => await identityLock.AcquireAsync("  ");

        act.Should().ThrowAsync<ArgumentException>();
    }
}
