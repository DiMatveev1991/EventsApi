using System.Collections.Concurrent;
using EventsApi.Application.Abstractions;

namespace EventsApi.Application.Services;

/// <summary>
/// Менеджер асинхронных per-event блокировок. Разные события не блокируют друг друга.
/// Неиспользуемые блокировки удаляются из словаря.
/// </summary>
public sealed class EventBookingLock : IEventBookingLock
{
    private readonly ConcurrentDictionary<Guid, LockEntry> _entries = new();

    public async ValueTask<IDisposable> AcquireAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        LockEntry entry;

        while (true)
        {
            entry = _entries.GetOrAdd(eventId, static _ => new LockEntry());

            lock (entry.SyncRoot)
            {
                if (entry.IsRetired)
                    continue;

                entry.ReferenceCount++;
                break;
            }
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            return new Releaser(this, eventId, entry);
        }
        catch
        {
            ReleaseReference(eventId, entry);
            throw;
        }
    }

    private void Release(Guid eventId, LockEntry entry)
    {
        entry.Semaphore.Release();
        ReleaseReference(eventId, entry);
    }

    private void ReleaseReference(Guid eventId, LockEntry entry)
    {
        lock (entry.SyncRoot)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount != 0)
                return;

            entry.IsRetired = true;
            _entries.TryRemove(new KeyValuePair<Guid, LockEntry>(eventId, entry));
        }
    }

    private sealed class LockEntry
    {
        public object SyncRoot { get; } = new();
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
        public bool IsRetired { get; set; }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly EventBookingLock _owner;
        private readonly Guid _eventId;
        private LockEntry? _entry;

        public Releaser(EventBookingLock owner, Guid eventId, LockEntry entry)
        {
            _owner = owner;
            _eventId = eventId;
            _entry = entry;
        }

        public void Dispose()
        {
            var entry = Interlocked.Exchange(ref _entry, null);
            if (entry is not null)
                _owner.Release(_eventId, entry);
        }
    }
}
