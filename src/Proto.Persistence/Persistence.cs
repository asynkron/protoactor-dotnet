// -----------------------------------------------------------------------
// <copyright file="Persistence.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Persistence;

[PublicAPI]
public class Persistence
{
    private readonly string _actorId;
    private readonly Action<Event>? _applyEvent;
    private readonly Action<Snapshot>? _applySnapshot;
    private readonly IEventStore _eventStore;
    private readonly Func<object>? _getState;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ISnapshotStrategy? _snapshotStrategy;

    private Persistence(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        string actorId,
        Action<Event>? applyEvent = null,
        Action<Snapshot>? applySnapshot = null,
        ISnapshotStrategy? snapshotStrategy = null,
        Func<object>? getState = null
    )
    {
        _eventStore = eventStore;
        _snapshotStore = snapshotStore;
        _actorId = actorId;
        _applyEvent = applyEvent;
        _applySnapshot = applySnapshot;
        _getState = getState;
        _snapshotStrategy = snapshotStrategy ?? new ManualSnapshots();
    }

    /// <summary>
    ///     Last event seen by this <see cref="Persistence" /> + 1, or -1 if no events have been seen
    /// </summary>
    public long Index { get; private set; } = -1;

    private bool UsingSnapshotting => _applySnapshot is not null; //TODO: why not used?
    private bool UsingEventSourcing => _applyEvent is not null;

    /// <summary>
    ///     Initializes <see cref="Persistence" /> with event storing only.
    /// </summary>
    /// <param name="eventStore"><see cref="IEventStore" /> to store events in</param>
    /// <param name="actorId">Unique actor id for which to store events</param>
    /// <param name="applyEvent">
    ///     A delegate that applies an event to actor's state. Used when events are stored, recovered or
    ///     replayed.
    /// </param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">Thrown when evenStore of applyEvent are null</exception>
    public static Persistence WithEventSourcing(IEventStore eventStore, string actorId, Action<Event> applyEvent)
    {
        if (eventStore is null)
        {
            throw new ArgumentNullException(nameof(eventStore));
        }

        if (applyEvent is null)
        {
            throw new ArgumentNullException(nameof(applyEvent));
        }

        return new Persistence(eventStore, new NoSnapshotStore(), actorId, applyEvent);
    }

    /// <summary>
    ///     Initializes <see cref="Persistence" /> with snapshotting only. No events are written.
    /// </summary>
    /// <param name="snapshotStore"><see cref="ISnapshotStore" /> to write snapshots to.</param>
    /// <param name="actorId">Unique actor id for which to store snapshots</param>
    /// <param name="applySnapshot">A delegate that applies snapshot to actor's state. Used every time snapshot is recovered.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">Thrown when snapshotStore or applySnapshot are null</exception>
    public static Persistence WithSnapshotting(
        ISnapshotStore snapshotStore,
        string actorId,
        Action<Snapshot> applySnapshot
    )
    {
        if (snapshotStore is null)
        {
            throw new ArgumentNullException(nameof(snapshotStore));
        }

        if (applySnapshot is null)
        {
            throw new ArgumentNullException(nameof(applySnapshot));
        }

        return new Persistence(new NoEventStore(), snapshotStore, actorId, null, applySnapshot);
    }

    /// <summary>
    ///     Initializes <see cref="Persistence" /> for storing events and snapshots. The snapshots are initiated manually.
    /// </summary>
    /// <param name="eventStore"><see cref="IEventStore" /> to store events in</param>
    /// <param name="snapshotStore"><see cref="ISnapshotStore" /> to write snapshots to.</param>
    /// <param name="actorId">Unique actor id for which to store events and snapshots</param>
    /// <param name="applyEvent">
    ///     A delegate that applies an event to actor's state. Used when events are stored, recovered or
    ///     replayed.
    /// </param>
    /// <param name="applySnapshot">A delegate that applies snapshot to actor's state. Used every time snapshot is recovered.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">Thrown when eventStore or applyEvent or snapshotStore or applySnapshot are null</exception>
    public static Persistence WithEventSourcingAndSnapshotting(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        string actorId,
        Action<Event> applyEvent,
        Action<Snapshot> applySnapshot
    )
    {
        if (eventStore is null)
        {
            throw new ArgumentNullException(nameof(eventStore));
        }

        if (snapshotStore is null)
        {
            throw new ArgumentNullException(nameof(snapshotStore));
        }

        if (applyEvent is null)
        {
            throw new ArgumentNullException(nameof(applyEvent));
        }

        if (applySnapshot is null)
        {
            throw new ArgumentNullException(nameof(applySnapshot));
        }

        return new Persistence(eventStore, snapshotStore, actorId, applyEvent, applySnapshot);
    }

    /// <summary>
    ///     Initializes <see cref="Persistence" /> for storing events and snapshots. The snapshots are initiated according to
    ///     <see cref="ISnapshotStrategy" />.
    /// </summary>
    /// <param name="eventStore"><see cref="IEventStore" /> to store events in</param>
    /// <param name="snapshotStore"><see cref="ISnapshotStore" /> to write snapshots to.</param>
    /// <param name="actorId">Unique actor id for which to store events and snapshots</param>
    /// <param name="applyEvent">
    ///     A delegate that applies an event to actor's state. Used when events are stored, recovered or
    ///     replayed.
    /// </param>
    /// <param name="applySnapshot">A delegate that applies snapshot to actor's state. Used every time snapshot is recovered.</param>
    /// <param name="snapshotStrategy">Snapshot strategy to use</param>
    /// <param name="getSnapshot">A delegate that return's snapshot of actor's state when it's time to store the snapshot</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when eventStore or applyEvent or snapshotStore or applySnapshot or
    ///     snapshotStrategy or getSnapshot are null
    /// </exception>
    public static Persistence WithEventSourcingAndSnapshotting(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        string actorId,
        Action<Event> applyEvent,
        Action<Snapshot> applySnapshot,
        ISnapshotStrategy snapshotStrategy,
        Func<object> getSnapshot
    )
    {
        if (eventStore is null)
        {
            throw new ArgumentNullException(nameof(eventStore));
        }

        if (snapshotStore is null)
        {
            throw new ArgumentNullException(nameof(snapshotStore));
        }

        if (applyEvent is null)
        {
            throw new ArgumentNullException(nameof(applyEvent));
        }

        if (applySnapshot is null)
        {
            throw new ArgumentNullException(nameof(applySnapshot));
        }

        if (snapshotStrategy is null)
        {
            throw new ArgumentNullException(nameof(snapshotStrategy));
        }

        if (getSnapshot is null)
        {
            throw new ArgumentNullException(nameof(getSnapshot));
        }

        return new Persistence(eventStore, snapshotStore, actorId, applyEvent, applySnapshot, snapshotStrategy,
            getSnapshot
        );
    }

    /// <summary>
    ///     Recovers the actor state to the latest known state from events and/or snapshots. Use this method when actor is
    ///     starting.
    /// </summary>
    /// <returns></returns>
    public async Task RecoverStateAsync()
    {
        var (snapshot, lastSnapshotIndex) = await _snapshotStore.GetSnapshotAsync(_actorId).ConfigureAwait(false);

        if (snapshot is not null && _applySnapshot is not null)
        {
            Index = lastSnapshotIndex;
            _applySnapshot(new RecoverSnapshot(snapshot, lastSnapshotIndex));
        }

        var fromEventIndex = Index + 1;

        await _eventStore.GetEventsAsync(
            _actorId,
            fromEventIndex,
            long.MaxValue,
            @event =>
            {
                Index++;
                _applyEvent?.Invoke(new RecoverEvent(@event, Index));
            }
        ).ConfigureAwait(false);
    }

    /// <summary>
    ///     Allows to replay events to rebuild state. For example, if we want to replay until just before
    ///     something happened (i.e. unexpected behavior of the system, bug, crash etc..) then apply some messages and observe
    ///     what happens.
    ///     Only possible if <see cref="Persistence" /> was configured to store events.
    /// </summary>
    /// <param name="fromIndex">Index of the first event to replay (inclusive)</param>
    /// <param name="toIndex">Index of the last event to replay (inclusive)</param>
    /// <exception cref="Exception">Thrown when this <see cref="Persistence" /> is not configured for storing events</exception>
    public async Task ReplayEvents(long fromIndex, long toIndex)
    {
        if (!UsingEventSourcing)
        {
            throw new Exception("Events cannot be replayed without using Event Sourcing.");
        }

        Index = fromIndex;

        await _eventStore.GetEventsAsync(
            _actorId,
            fromIndex,
            toIndex,
            @event =>
            {
                _applyEvent?.Invoke(new ReplayEvent(@event, Index));
                Index++;
            }
        ).ConfigureAwait(false);
    }

    /// <summary>
    ///     Persists the event and optionally a snapshot if <see cref="ISnapshotStrategy" /> is configured. The event will also
    ///     be applied to actor state
    ///     with the delegate configured for this <see cref="Persistence" />
    /// </summary>
    /// <param name="event"></param>
    /// <exception cref="Exception"></exception>
    public async Task PersistEventAsync(object @event)
    {
        if (!UsingEventSourcing)
        {
            throw new Exception("Event cannot be persisted without using Event Sourcing.");
        }

        var persistedEvent = new PersistedEvent(@event, Index + 1);

        await _eventStore.PersistEventAsync(_actorId, persistedEvent.Index, persistedEvent.Data).ConfigureAwait(false);

        Index++;

        _applyEvent?.Invoke(persistedEvent);

        if (_snapshotStrategy?.ShouldTakeSnapshot(persistedEvent) == true && _getState is not null)
        {
            var persistedSnapshot = new PersistedSnapshot(_getState(), persistedEvent.Index);

            await _snapshotStore.PersistSnapshotAsync(_actorId, persistedSnapshot.Index, persistedSnapshot.State).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Stores a snapshot of actor's state
    /// </summary>
    /// <param name="snapshot">Snapshot to store</param>
    public async Task PersistSnapshotAsync(object snapshot)
    {
        var persistedSnapshot = new PersistedSnapshot(snapshot, Index + 1);
        await _snapshotStore.PersistSnapshotAsync(_actorId, persistedSnapshot.Index, snapshot).ConfigureAwait(false);
        Index++;
    }

    /// <summary>
    ///     Deletes snapshots for the this actor
    /// </summary>
    /// <param name="inclusiveToIndex">
    ///     Index stored along the snapshot has to be &lt;= to the value in this parameter for the
    ///     snapshot to be deleted
    /// </param>
    /// <returns></returns>
    public Task DeleteSnapshotsAsync(long inclusiveToIndex) =>
        _snapshotStore.DeleteSnapshotsAsync(_actorId, inclusiveToIndex);

    /// <summary>
    ///     Deletes events from actor's event stream starting with the oldest available, ending at provided index
    /// </summary>
    /// <param name="inclusiveToIndex">Inclusive index of the last event to delete</param>
    /// <returns></returns>
    public Task DeleteEventsAsync(long inclusiveToIndex) => _eventStore.DeleteEventsAsync(_actorId, inclusiveToIndex);

    /// <summary>
    ///     A <see cref="ISnapshotStrategy" /> that will not cause any snapshots to be stored. User should manually store
    ///     snapshots with <see cref="Persistence.PersistSnapshotAsync(object)" />
    /// </summary>
    private class ManualSnapshots : ISnapshotStrategy
    {
        public bool ShouldTakeSnapshot(PersistedEvent persistedEvent) => false;
    }

    private class NoEventStore : IEventStore
    {
        public Task<long>
            GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback) =>
            Task.FromResult(-1L);

        public Task<long> PersistEventAsync(string actorName, long index, object @event) => Task.FromResult(0L);

        public Task DeleteEventsAsync(string actorName, long inclusiveToIndex) => Task.CompletedTask;
    }

    private class NoSnapshotStore : ISnapshotStore
    {
        public Task<(object? Snapshot, long Index)> GetSnapshotAsync(string actorName) =>
            Task.FromResult<(object? Snapshot, long Index)>((null, 0));

        public Task PersistSnapshotAsync(string actorName, long index, object snapshot) => Task.FromResult(0);

        public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex) => Task.FromResult(0);
    }
}