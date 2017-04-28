// -----------------------------------------------------------------------
//  <copyright file="Persistence.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto.Persistence
{
    public class Persistence
    {
        public long Index { get; private set; }
        private readonly IProviderState _state;
        private readonly Action<Event> _applyEvent;
        private readonly Action<Snapshot> _applySnapshot;
        private readonly Func<object> _getState;
        private readonly ISnapshotStrategy _snapshotStrategy;
        private bool UsingSnapshotting => _applySnapshot != null;
        private bool UsingEventSourcing => _applyEvent != null;
        private readonly string _actorId;

        private Persistence(IProvider provider, string actorId, Action<Event> applyEvent = null, 
            Action<Snapshot> applySnapshot = null, ISnapshotStrategy snapshotStrategy = null, Func<object> getState = null)
        {
            _actorId = actorId;
            _state = provider.GetState();
            _applyEvent = applyEvent;
            _applySnapshot = applySnapshot;
            _getState = getState;
            _snapshotStrategy = snapshotStrategy ?? new NoSnapshots();
        }

        public static Persistence WithEventSourcing(IProvider provider, string actorId, Action<Event> applyEvent)
        {
            if (applyEvent == null) throw new ArgumentNullException(nameof(applyEvent));
            return new Persistence(provider, actorId, applyEvent);
        }

        public static Persistence WithSnapshotting(IProvider provider, string actorId, Action<Snapshot> applySnapshot)
        {
            if (applySnapshot == null) throw new ArgumentNullException(nameof(applySnapshot));
            return new Persistence(provider, actorId, null, applySnapshot);
        }

        public static Persistence WithEventSourcingAndSnapshotting(IProvider provider, string actorId, Action<Event> applyEvent, Action<Snapshot> applySnapshot)
        {
            if (applyEvent == null) throw new ArgumentNullException(nameof(applyEvent));
            if (applySnapshot == null) throw new ArgumentNullException(nameof(applySnapshot));
            return new Persistence(provider, actorId, applyEvent, applySnapshot);
        }

        public static Persistence WithEventSourcingAndSnapshotting(IProvider provider, string actorId, Action<Event> applyEvent, 
            Action<Snapshot> applySnapshot, ISnapshotStrategy snapshotStrategy, Func<object> getState)
        {
            if (applyEvent == null) throw new ArgumentNullException(nameof(applyEvent));
            if (applySnapshot == null) throw new ArgumentNullException(nameof(applySnapshot));
            if (snapshotStrategy == null) throw new ArgumentNullException(nameof(snapshotStrategy));
            if (getState == null) throw new ArgumentNullException(nameof(getState));
            return new Persistence(provider, actorId, applyEvent, applySnapshot, snapshotStrategy, getState);
        }

        public async Task RecoverStateAsync()
        {
            if (UsingSnapshotting)
            {
                var (snapshot, index) = await _state.GetSnapshotAsync(_actorId);

                if (snapshot != null)
                {
                    Index = index;
                    _applySnapshot(new RecoverSnapshot(snapshot, index));
                }
            }

            if (UsingEventSourcing)
            {
                await _state.GetEventsAsync(_actorId, Index, @event =>
                {
                    Index++;
                    _applyEvent(new RecoverEvent(@event, Index));
                });
            }
        }
        
        public async Task PersistEventAsync(object @event)
        {
            if (!UsingEventSourcing)
            {
                throw new Exception("Event cannot be persisted without using Event Sourcing.");
            }
            Index++;
            await _state.PersistEventAsync(_actorId, Index, @event);
            var persistedEvent = new PersistedEvent(@event, Index);
            _applyEvent(persistedEvent);
            if (_snapshotStrategy.ShouldTakeSnapshot(persistedEvent))
            {
                await _state.PersistSnapshotAsync(_actorId, Index, _getState());
            }
        }

        public async Task PersistSnapshotAsync(object snapshot)
        {
            await _state.PersistSnapshotAsync(_actorId, Index, snapshot);
        }

        public async Task DeleteSnapshotsAsync(long inclusiveToIndex)
        {
            await _state.DeleteSnapshotsAsync(_actorId, inclusiveToIndex);
        }

        public async Task DeleteEventsAsync(long inclusiveToIndex)
        {
            await _state.DeleteEventsAsync(_actorId, inclusiveToIndex);
        }

        private class NoSnapshots : ISnapshotStrategy
        {
            public bool ShouldTakeSnapshot(PersistedEvent persistedEvent)
            {
                return false;
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

    public class PersistedEvent : Event
    {
        public PersistedEvent(object data, long index) : base(data, index)
        {
        }
    }
}