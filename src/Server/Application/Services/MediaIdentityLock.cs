using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Services;

/// <summary>
/// In-process, reference-counted lock keyed by media identity.
/// </summary>
/// <remarks>
/// Entries are reference counted and removed once nobody waits on them, so a large scan does not leave one
/// semaphore per album behind.
/// </remarks>
public sealed class MediaIdentityLock : IMediaIdentityLock
{
    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        /// <summary>Holders plus waiters. Guarded by the dictionary lock, never by the semaphore.</summary>
        public int ReferenceCount { get; set; }
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _entriesLock = new();

    public async Task<IAsyncDisposable> AcquireAsync(string identityKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityKey);

        Entry entry;
        lock (_entriesLock)
        {
            if (!_entries.TryGetValue(identityKey, out var existing))
            {
                existing = new Entry();
                _entries[identityKey] = existing;
            }

            existing.ReferenceCount++;
            entry = existing;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
        }
        catch
        {
            // Never became a holder, so drop the reservation taken above.
            Release(identityKey, entry, releaseSemaphore: false);
            throw;
        }

        return new Handle(this, identityKey, entry);
    }

    private void Release(string identityKey, Entry entry, bool releaseSemaphore)
    {
        if (releaseSemaphore)
        {
            entry.Semaphore.Release();
        }

        lock (_entriesLock)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount > 0)
                return;

            // Only remove the entry we reserved: a concurrent acquire may already have replaced it.
            if (_entries.TryGetValue(identityKey, out var current) && ReferenceEquals(current, entry))
            {
                _entries.Remove(identityKey);
            }
        }

        entry.Semaphore.Dispose();
    }

    private sealed class Handle(MediaIdentityLock owner, string identityKey, Entry entry) : IAsyncDisposable
    {
        private bool _disposed;

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _disposed = true;
            owner.Release(identityKey, entry, releaseSemaphore: true);
            return ValueTask.CompletedTask;
        }
    }
}
