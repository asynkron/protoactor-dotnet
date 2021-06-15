// -----------------------------------------------------------------------
// <copyright file="Persistence.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Persistence
{
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

        public long Index { get; private set; } = -1;
        private bool UsingSnapshotting => _applySnapshot is not null; //TODO: why not used?
        private bool UsingEventSourcing => _applyEvent is not null;

        public static Persistence WithEventSourcing(IEventStore eventStore, string actorId, Action<Event> applyEvent)
        {
            if (eventStore is null) throw new ArgumentNullException(nameof(eventStore));

            if (applyEvent is null) throw new ArgumentNullException(nameof(applyEvent));

            return new Persistence(eventStore, new NoSnapshotStore(), actorId, applyEvent);
        }

        public static Persistence WithSnapshotting(
            ISnapshotStore snapshotStore,
            string actorId,
            Action<Snapshot> applySnapshot
        )
        {
            if (snapshotStore is null) throw new ArgumentNullException(nameof(snapshotStore));

            if (applySnapshot is null) throw new ArgumentNullException(nameof(applySnapshot));

            return new Persistence(new NoEventStore(), snapshotStore, actorId, null, applySnapshot);
        }

        public static Persistence WithEventSourcingAndSnapshotting(
            IEventStore eventStore,
            ISnapshotStore snapshotStore,
            string actorId,
            Action<Event> applyEvent,
            Action<Snapshot> applySnapshot
        )
        {
            if (eventStore is null) throw new ArgumentNullException(nameof(eventStore));

            if (snapshotStore is null) throw new ArgumentNullException(nameof(snapshotStore));

            if (applyEvent is null) throw new ArgumentNullException(nameof(applyEvent));

            if (applySnapshot is null) throw new ArgumentNullException(nameof(applySnapshot));

            return new Persistence(eventStore, snapshotStore, actorId, applyEvent, applySnapshot);
        }

        public static Persistence WithEventSourcingAndSnapshotting(
            IEventStore eventStore,
            ISnapshotStore snapshotStore,
            string actorId,
            Action<Event> applyEvent,
            Action<Snapshot> applySnapshot,
            ISnapshotStrategy snapshotStrategy,
            Func<object> getState
        )
        {
            if (eventStore is null) throw new ArgumentNullException(nameof(eventStore));

            if (snapshotStore is null) throw new ArgumentNullException(nameof(snapshotStore));

            if (applyEvent is null) throw new ArgumentNullException(nameof(applyEvent));

            if (applySnapshot is null) throw new ArgumentNullException(nameof(applySnapshot));

            if (snapshotStrategy is null) throw new ArgumentNullException(nameof(snapshotStrategy));

            if (getState is null) throw new ArgumentNullException(nameof(getState));

            return new Persistence(eventStore, snapshotStore, actorId, applyEvent, applySnapshot, snapshotStrategy,
                getState
            );
        }

        /// <summary>
        ///     Recovers the actor to the latest state
        /// </summary>
        /// <returns></returns>
        public async Task RecoverStateAsync()
        {
            var (snapshot, lastSnapshotIndex) = await _snapshotStore.GetSnapshotAsync(_actorId);

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
                @event => {
                    Index++;
                    _applyEvent?.Invoke(new RecoverEvent(@event, Index));
                }
            );
        }

        /// <summary>
        ///     Allows the replaying of events to rebuild state from a range. For example, if we want to replay until just before
        ///     something happened
        ///     (i.e. unexpected behavior of the system, bug, crash etc..) then apply some messages and observe what happens.
        /// </summary>
        public async Task ReplayEvents(long fromIndex, long toIndex)
        {
            if (!UsingEventSourcing) throw new Exception("Events cannot be replayed without using Event Sourcing.");

            Index = fromIndex;

            await _eventStore.GetEventsAsync(
                _actorId,
                fromIndex,
                toIndex,
                @event => {
                    _applyEvent?.Invoke(new ReplayEvent(@event, Index));
                    Index++;
                }
            );
        }

        public async Task PersistEventAsync(object @event)
        {
            if (!UsingEventSourcing) throw new Exception("Event cannot be persisted without using Event Sourcing.");

            var persistedEvent = new PersistedEvent(@event, Index + 1);

            await _eventStore.PersistEventAsync(_actorId, persistedEvent.Index, persistedEvent.Data);

            Index++;

            _applyEvent?.Invoke(persistedEvent);

            if (_snapshotStrategy?.ShouldTakeSnapshot(persistedEvent) == true && _getState is not null)
            {
                var persistedSnapshot = new PersistedSnapshot(_getState(), persistedEvent.Index);

                await _snapshotStore.PersistSnapshotAsync(_actorId, persistedSnapshot.Index, persistedSnapshot.State);
            }
        }

        public async Task PersistSnapshotAsync(object snapshot)
        {
            var persistedSnapshot = new PersistedSnapshot(snapshot, Index+1);
            await _snapshotStore.PersistSnapshotAsync(_actorId, persistedSnapshot.Index, snapshot);
            Index++;
        }

        public Task DeleteSnapshotsAsync(long inclusiveToIndex) =>
            _snapshotStore.DeleteSnapshotsAsync(_actorId, inclusiveToIndex);

        public Task DeleteEventsAsync(long inclusiveToIndex) =>
            _eventStore.DeleteEventsAsync(_actorId, inclusiveToIndex);

        [Obsolete("Use ManualSnapshots instead", false)]
        private class NoSnapshots : ISnapshotStrategy
        {
            public bool ShouldTakeSnapshot(PersistedEvent persistedEvent) => false;
        }
        
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
            public Task<(object? Snapshot, long Index)> GetSnapshotAsync(string actorName)
                => Task.FromResult<(object? Snapshot, long Index)>((null, 0));

            public Task PersistSnapshotAsync(string actorName, long index, object snapshot) => Task.FromResult(0);

            public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex) => Task.FromResult(0);
        }
    }
}