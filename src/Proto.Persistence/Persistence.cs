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
        private readonly string _persistenceId;

        /// <summary>
        /// Provides Event Sourcing with optional snapshotting functionality to persist an actor's state.
        /// </summary>
        /// <param name="provider">The database provider to use for persistence</param>
        /// <param name="persistenceId">The id of the actor. This should be a unique string</param>
        public Persistence(IProvider provider, string persistenceId)
        {
            _persistenceId = persistenceId;
            _state = provider.GetState();
        }

        public async Task RecoverStateFromEventsAsync(Action<Event> applyEvent)
        {
            if (applyEvent == null)
            {
                throw new ArgumentNullException(nameof(applyEvent));
            }
            await _state.GetEventsAsync(_persistenceId, Index, @event =>
            {
                Index++;
                applyEvent(new RecoverEvent(@event, Index));
            });
        }

        public async Task RecoverStateFromSnapshotAsync(Action<Snapshot> applySnapshot)
        {
            if (applySnapshot == null)
            {
                throw new ArgumentNullException(nameof(applySnapshot));
            }
            var (snapshot, index) = await _state.GetSnapshotAsync(_persistenceId);

            if (snapshot != null)
            {
                Index = index;
                applySnapshot(new RecoverSnapshot(snapshot, index));
            }
        }

        public async Task RecoverStateFromSnapshotThenEventsAsync(Action<Snapshot> applySnapshot, Action<Event> applyEvent)
        {
            await RecoverStateFromSnapshotAsync(applySnapshot);
            await RecoverStateFromEventsAsync(applyEvent);
        }
        
        public async Task PersistEventAsync(object @event, Action<Event> applyEvent)
        {
            if (applyEvent == null)
            {
                throw new ArgumentNullException(nameof(applyEvent));
            }
            Index++;
            await _state.PersistEventAsync(_persistenceId, Index, @event);
            applyEvent(new PersistedEvent(@event, Index));
        }

        public async Task PersistSnapshotAsync(object snapshot)
        {
            await _state.PersistSnapshotAsync(_persistenceId, Index, snapshot);
        }

        public async Task DeleteSnapshotsAsync(long inclusiveToIndex)
        {
            await _state.DeleteSnapshotsAsync(_persistenceId, inclusiveToIndex);
        }

        public async Task DeleteEventsAsync(long inclusiveToIndex)
        {
            await _state.DeleteEventsAsync(_persistenceId, inclusiveToIndex);
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