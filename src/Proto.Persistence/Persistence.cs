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
        private bool UsingSnapshotting => _applySnapshot != null;
        private readonly string _actorId;

        /// <summary>
        /// Provides Event Sourcing with optional snapshotting functionality to persist an actor's state.
        /// </summary>
        /// <param name="provider">The database provider to use for persistence</param>
        /// <param name="actorId">The id of the actor. This should be a unique string</param>
        /// <param name="applyEvent">The function to call when an event is either saved to the database or replayed on loading. This
        /// should be used only to mutate actor state</param>
        /// <param name="applySnapshot">The function to call when a snapshot is found on loading. This should be used to set actor state</param>
        public Persistence(IProvider provider, string actorId, Action<Event> applyEvent, Action<Snapshot> applySnapshot = null)
        {
            _actorId = actorId;
            _state = provider.GetState();
            _applySnapshot = applySnapshot;
            _applyEvent = applyEvent;
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
            await _state.GetEventsAsync(_actorId, Index, @event =>
            {
                Index++;
                _applyEvent(new RecoverEvent(@event, Index));
            });
        }
        
        public async Task PersistEventAsync(object @event)
        {
            Index++;
            await _state.PersistEventAsync(_actorId, Index, @event);
            _applyEvent(new PersistedEvent(@event, Index));
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