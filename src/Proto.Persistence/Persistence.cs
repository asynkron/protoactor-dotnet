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
        private readonly IProviderState _state;
        private Action<Event> _applyEvent;
        private Action<Snapshot> _applySnapshot;

        public long Index { get; private set; }
        private IContext _context;
        private string ActorId => _context.Self.Id;

        public Persistence(IProvider provider)
        {
            _state = provider.GetState();
        }

        public async Task InitAsync(IContext context, Action<Event> applyEvent = null, Action<Snapshot> applySnapshot = null)
        {
            _context = context;
            _applyEvent = applyEvent;
            _applySnapshot = applySnapshot;

            if (applySnapshot != null)
            {
                var (snapshot, index) = await _state.GetSnapshotAsync(ActorId);

                if (snapshot != null)
                {
                    Index = index;
                    _applySnapshot(new RecoverSnapshot(snapshot, index));
                }
            }

            if (_applyEvent != null)
            {
                await _state.GetEventsAsync(ActorId, Index, @event =>
                {
                    Index++;
                    _applyEvent(new RecoverEvent(@event, Index));
                });
            }
        }

        public async Task PersistEventAsync(object @event)
        {
            Index++;
            await _state.PersistEventAsync(ActorId, Index, @event);
            _applyEvent(new PersistedEvent(@event, Index));
        }

        public async Task PersistSnapshotAsync(object snapshot)
        {
            await _state.PersistSnapshotAsync(ActorId, Index, snapshot);
        }

        public async Task DeleteSnapshotsAsync(long inclusiveToIndex)
        {
            await _state.DeleteSnapshotsAsync(ActorId, inclusiveToIndex);
        }

        public async Task DeleteEventsAsync(long inclusiveToIndex)
        {
            await _state.DeleteEventsAsync(ActorId, inclusiveToIndex);
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