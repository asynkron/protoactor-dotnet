// -----------------------------------------------------------------------
//  <copyright file="Persistence.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto.Persistence
{
    public class Persistence
    {
        public long Index { get; private set; } = -1;
        private readonly Action<Event> _applyEvent;
        private readonly Action<Snapshot> _applySnapshot;
        private readonly Func<object> _getState;
        private readonly ISnapshotStrategy _snapshotStrategy;
        private bool UsingSnapshotting => _applySnapshot != null; //TODO: why not used?
        private bool UsingEventSourcing => _applyEvent != null;
        private readonly IEventStore _eventStore;
        private readonly ISnapshotStore _snapshotStore;
        private readonly string _actorId;

        private Persistence(IEventStore eventStore, ISnapshotStore snapshotStore, string actorId, Action<Event> applyEvent = null, 
            Action<Snapshot> applySnapshot = null, ISnapshotStrategy snapshotStrategy = null, Func<object> getState = null)
        {
            _eventStore = eventStore;
            _snapshotStore = snapshotStore;
            _actorId = actorId;
            _applyEvent = applyEvent;
            _applySnapshot = applySnapshot;
            _getState = getState;
            _snapshotStrategy = snapshotStrategy ?? new NoSnapshots();
        }

        public static Persistence WithEventSourcing(IEventStore eventStore, string actorId, Action<Event> applyEvent)
        {
            if (eventStore == null) throw new ArgumentNullException(nameof(eventStore));
            if (applyEvent == null) throw new ArgumentNullException(nameof(applyEvent));
            return new Persistence(eventStore, new NoSnapshotStore(), actorId, applyEvent);
        }

        public static Persistence WithSnapshotting(ISnapshotStore snapshotStore, string actorId, Action<Snapshot> applySnapshot)
        {
            if (snapshotStore == null) throw new ArgumentNullException(nameof(snapshotStore));
            if (applySnapshot == null) throw new ArgumentNullException(nameof(applySnapshot));
            return new Persistence(new NoEventStore(), snapshotStore, actorId, null, applySnapshot);
        }

        public static Persistence WithEventSourcingAndSnapshotting(IEventStore eventStore, ISnapshotStore snapshotStore, string actorId, Action<Event> applyEvent, Action<Snapshot> applySnapshot)
        {
            if (eventStore == null) throw new ArgumentNullException(nameof(eventStore));
            if (snapshotStore == null) throw new ArgumentNullException(nameof(snapshotStore));
            if (applyEvent == null) throw new ArgumentNullException(nameof(applyEvent));
            if (applySnapshot == null) throw new ArgumentNullException(nameof(applySnapshot));
            return new Persistence(eventStore, snapshotStore, actorId, applyEvent, applySnapshot);
        }

        public static Persistence WithEventSourcingAndSnapshotting(IEventStore eventStore, ISnapshotStore snapshotStore, string actorId, Action<Event> applyEvent, 
            Action<Snapshot> applySnapshot, ISnapshotStrategy snapshotStrategy, Func<object> getState)
        {
            if (eventStore == null) throw new ArgumentNullException(nameof(eventStore));
            if (snapshotStore == null) throw new ArgumentNullException(nameof(snapshotStore));
            if (applyEvent == null) throw new ArgumentNullException(nameof(applyEvent));
            if (applySnapshot == null) throw new ArgumentNullException(nameof(applySnapshot));
            if (snapshotStrategy == null) throw new ArgumentNullException(nameof(snapshotStrategy));
            if (getState == null) throw new ArgumentNullException(nameof(getState));
            return new Persistence(eventStore, snapshotStore, actorId, applyEvent, applySnapshot, snapshotStrategy, getState);
        }
        
        /// <summary>
        /// Recovers the actor to the latest state
        /// </summary>
        /// <returns></returns>
        public async Task RecoverStateAsync()
        {
            var (snapshot, lastSnapshotIndex) = await _snapshotStore.GetSnapshotAsync(_actorId);

            if (snapshot != null)
            {
                Index = lastSnapshotIndex;
                _applySnapshot(new RecoverSnapshot(snapshot, lastSnapshotIndex));
            }

            var fromEventIndex = Index + 1;
            
            await _eventStore.GetEventsAsync(_actorId, fromEventIndex, long.MaxValue, @event =>
            {
                Index++;
                _applyEvent(new RecoverEvent(@event, Index));
            });
        }

        /// <summary>
        /// Allows the replaying of events to rebuild state from a range. For example, if we want to replay until just before something happened 
        /// (i.e. unexpected behavior of the system, bug, crash etc..) then apply some messages and observe what happens.
        /// </summary>
        public async Task ReplayEvents(long fromIndex, long toIndex)
        {
            if (!UsingEventSourcing)
            {
                throw new Exception("Events cannot be replayed without using Event Sourcing.");
            }

            Index = fromIndex;

            await _eventStore.GetEventsAsync(_actorId, fromIndex, toIndex, @event =>
            {
                _applyEvent(new ReplayEvent(@event, Index));
                Index++;
            });
        }

        public async Task PersistEventAsync(object @event)
        {
            if (!UsingEventSourcing)
            {
                throw new Exception("Event cannot be persisted without using Event Sourcing.");
            }

            var persistedEvent = new PersistedEvent(@event, (Index + 1));

            await _eventStore.PersistEventAsync(_actorId, persistedEvent.Index, persistedEvent.Data);
            
            Index++;

            _applyEvent(persistedEvent);

            if (_snapshotStrategy.ShouldTakeSnapshot(persistedEvent))
            {
                var persistedSnapshot = new PersistedSnapshot(_getState(), persistedEvent.Index);

                await _snapshotStore.PersistSnapshotAsync(_actorId, persistedSnapshot.Index, persistedSnapshot.State);
            }
        }

        public async Task PersistSnapshotAsync(object snapshot)
        {
            var persistedSnapshot = new PersistedSnapshot(snapshot, Index);

            await _snapshotStore.PersistSnapshotAsync(_actorId, persistedSnapshot.Index, snapshot);
        }

        public async Task DeleteSnapshotsAsync(long inclusiveToIndex)
        {
            await _snapshotStore.DeleteSnapshotsAsync(_actorId, inclusiveToIndex);
        }

        public async Task DeleteEventsAsync(long inclusiveToIndex)
        {
            await _eventStore.DeleteEventsAsync(_actorId, inclusiveToIndex);
        }

        private class NoSnapshots : ISnapshotStrategy
        {
            public bool ShouldTakeSnapshot(PersistedEvent persistedEvent)
            {
                return false;
            }
        }
        
        private class NoEventStore : IEventStore
        {
            public Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
            {
                return Task.FromResult(-1L);
            }

            public Task<long> PersistEventAsync(string actorName, long index, object @event)
            {
                return Task.FromResult(0L);
            }

            public Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
            {
                return Task.FromResult(0);
            }
        }

        private class NoSnapshotStore : ISnapshotStore
        {
            public Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
            {
                return Task.FromResult<(object Snapshot, long Index)>((null, 0));
            }

            public Task PersistSnapshotAsync(string actorName, long index, object snapshot)
            {
                return Task.FromResult(0);
            }

            public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
            {
                return Task.FromResult(0);
            }
        }
    }

    public class Snapshot
    {
        public object State { get; }
        public long Index { get; }

        public Snapshot(object state, long index)
        {
            State = state;
            Index = index;
        }
    }
    public class RecoverSnapshot : Snapshot
    {
        public RecoverSnapshot(object state, long index) : base(state, index)
        {
        }
    }

    public class PersistedSnapshot : Snapshot
    {
        public PersistedSnapshot(object state, long index) : base(state, index)
        {
        }
    }

    public class Event
    {
        public object Data { get; }
        public long Index { get; }

        public Event(object data, long index)
        {
            Data = data;
            Index = index;
        }
    }
    public class RecoverEvent : Event
    {
        public RecoverEvent(object data, long index) : base(data, index)
        {
        }
    }

    public class ReplayEvent : Event
    {
        public ReplayEvent(object data, long index) : base(data, index)
        {
        }
    }

    public class PersistedEvent : Event
    {
        public PersistedEvent(object data, long index) : base(data, index)
        {
        }
    }
}