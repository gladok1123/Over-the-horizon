using System.Collections.Concurrent;

namespace OverHorizon.Modding.Events;

/// <summary>
/// Thread-safe, lock-free copy-on-write implementation of <see cref="IEventBus"/>.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, object> _channels = new();
    private long _nextRegistrationId;

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(
        GameEventAction<TEvent> callback,
        EventPriority priority = EventPriority.Normal,
        bool ignoreCancelled = true)
        where TEvent : struct, IGameEvent
    {
        ArgumentNullException.ThrowIfNull(callback);

        var channel = GetChannel<TEvent>();
        var id = Interlocked.Increment(ref _nextRegistrationId);
        return channel.Add(id, priority, ignoreCancelled, callback);
    }

    /// <inheritdoc />
    public void Publish<TEvent>(ref TEvent payload)
        where TEvent : struct, IGameEvent
    {
        if (_channels.TryGetValue(typeof(TEvent), out var rawChannel))
        {
            ((TypedChannel<TEvent>)rawChannel).Dispatch(ref payload);
        }
    }

    private TypedChannel<TEvent> GetChannel<TEvent>()
        where TEvent : struct, IGameEvent =>
        (TypedChannel<TEvent>)_channels.GetOrAdd(typeof(TEvent), static _ => new TypedChannel<TEvent>());

    private sealed class TypedChannel<TEvent>
        where TEvent : struct, IGameEvent
    {
        private static readonly bool IsCancellable = typeof(ICancellableEvent).IsAssignableFrom(typeof(TEvent));

        private SubscriberEntry[] _subscribers = [];

        public IDisposable Add(
            long id,
            EventPriority priority,
            bool ignoreCancelled,
            GameEventAction<TEvent> callback)
        {
            var entry = new SubscriberEntry(id, priority, ignoreCancelled, callback);

            while (true)
            {
                var snapshot = Volatile.Read(ref _subscribers);
                var updated = new SubscriberEntry[snapshot.Length + 1];
                Array.Copy(snapshot, updated, snapshot.Length);
                updated[^1] = entry;
                Array.Sort(updated);

                if (ReferenceEquals(Interlocked.CompareExchange(ref _subscribers, updated, snapshot), snapshot))
                {
                    return new SubscriptionToken(this, id);
                }
            }
        }

        public void Remove(long id)
        {
            while (true)
            {
                var snapshot = Volatile.Read(ref _subscribers);
                var idx = -1;
                for (var i = 0; i < snapshot.Length; i++)
                {
                    if (snapshot[i].RegistrationId == id)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx < 0)
                {
                    return;
                }

                SubscriberEntry[] updated;
                if (snapshot.Length == 1)
                {
                    updated = [];
                }
                else
                {
                    updated = new SubscriberEntry[snapshot.Length - 1];
                    if (idx > 0)
                    {
                        Array.Copy(snapshot, 0, updated, 0, idx);
                    }

                    if (idx < snapshot.Length - 1)
                    {
                        Array.Copy(snapshot, idx + 1, updated, idx, snapshot.Length - idx - 1);
                    }
                }

                if (ReferenceEquals(Interlocked.CompareExchange(ref _subscribers, updated, snapshot), snapshot))
                {
                    return;
                }
            }
        }

        public void Dispatch(ref TEvent payload)
        {
            var list = Volatile.Read(ref _subscribers);
            for (var i = 0; i < list.Length; i++)
            {
                ref readonly var sub = ref list[i];
                if (IsCancellable &&
                    sub.IgnoreCancelled &&
                    sub.Priority != EventPriority.Monitor &&
                    ((ICancellableEvent)payload).IsCancelled)
                {
                    continue;
                }

                sub.Callback(ref payload);
            }
        }

        private readonly record struct SubscriberEntry(
            long RegistrationId,
            EventPriority Priority,
            bool IgnoreCancelled,
            GameEventAction<TEvent> Callback) : IComparable<SubscriberEntry>
        {
            public int CompareTo(SubscriberEntry other)
            {
                var prioCmp = Priority.CompareTo(other.Priority);
                return prioCmp != 0 ? prioCmp : RegistrationId.CompareTo(other.RegistrationId);
            }
        }

        private sealed class SubscriptionToken(TypedChannel<TEvent> owner, long id) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    owner.Remove(id);
                }
            }
        }
    }
}
