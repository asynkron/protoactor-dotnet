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
        private IEventState _eventState;
        private ISnapshotState _snapshotState;
        public long Index { get; private set; }
        private IContext _context;
        private string ActorId => _context.Self.Id;

        public async Task InitAsync(IProvider provider, IContext context)
        {
            _eventState = provider.GetEventState();
            _snapshotState = provider.GetSnapshotState();
            _context = context;

            await _context.ReceiveAsync(new RecoveryStarted());

            var (snapshot, index) = await _snapshotState.GetSnapshotAsync(ActorId);

            if (snapshot != null)
            {
                Index = index;
                await _context.ReceiveAsync(new RecoverSnapshot(snapshot));
            };

            await _eventState.GetEventsAsync(ActorId, Index, @event =>
            {
                _context.ReceiveAsync(new RecoverEvent(@event)).Wait();
                Index++;
            });
            
            await _context.ReceiveAsync(new RecoveryCompleted());
        }

        public async Task PersistEventAsync(object @event)
        {
            var index = Index;
            await _eventState.PersistEventAsync(ActorId, index, @event);
            Index++;
            await _context.ReceiveAsync(new PersistedEvent(index, @event));
        }

        public async Task PersistSnapshotAsync(object snapshot)
        {
            var index = Index;
            await _snapshotState.PersistSnapshotAsync(ActorId, index, snapshot);
            await _context.ReceiveAsync(new PersistedSnapshot(index, snapshot));
        }

        public async Task DeleteSnapshotsAsync(long inclusiveToIndex)
        {
            await _snapshotState.DeleteSnapshotsAsync(ActorId, inclusiveToIndex);
        }

        public async Task DeleteEventsAsync(long inclusiveToIndex)
        {
            await _eventState.DeleteEventsAsync(ActorId, inclusiveToIndex);
        }

        public static Func<Receive, Receive> Using(IProvider provider)
        {
            return next => async context =>
            {
                switch (context.Message)
                {
                    case Started _:
                        if(context.Actor is IPersistentActor actor)
                        {
                            actor.Persistence = new Persistence();
                            await actor.Persistence.InitAsync(provider, context);
                        }
                        break;
                }
                await next(context);
            };
        }
    }

    public class RequestSnapshot { }

    public class RecoverSnapshot
    {
        public RecoverSnapshot(object snapshot)
        {
            Snapshot = snapshot;
        }

        public object Snapshot { get; }
    }

    public class RecoverEvent
    {
        public RecoverEvent(object @event)
        {
            Event = @event;
        }
        
        public object Event { get; }
    }

    public class RecoveryStarted { }
    public class RecoveryCompleted { }

    public class PersistedEvent
    {
        public PersistedEvent(long index, object @event)
        {
            Index = index;
            Event = @event;
        }

        public long Index { get; }
        public object Event { get; }
    }

    public class PersistedSnapshot
    {
        public PersistedSnapshot(long index, object snapshot)
        {
            Index = index;
            Snapshot = snapshot;
        }

        public long Index { get; }
        public object Snapshot { get; }
    }
}