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
        private IProviderState _state;
        public long Index { get; private set; }
        private IContext _context;
        private string ActorId => _context.Self.Id;
        private static bool _initialized = false;

        public async Task InitAsync(IProvider provider, IContext context)
        {
            _state = provider.GetState();
            _context = context;

            _context.Self.Tell(new RecoveryStarted());

            var (snapshot, index) = await _state.GetSnapshotAsync(ActorId);

            if (snapshot != null)
            {
                Index = index;
                _context.Self.Tell(new RecoverSnapshot(snapshot));
            };

            await _state.GetEventsAsync(ActorId, Index, @event =>
            {
                _context.Self.Tell(new RecoverEvent(@event));
                Index++;
            });

            _context.Self.Tell(new RecoveryCompleted());
        }

        public async Task PersistEventAsync(object @event)
        {
            var index = Index;
            await _state.PersistEventAsync(ActorId, index, @event);
            Index++;
            _context.Self.Tell(new PersistedEvent(index, @event));
        }

        public async Task PersistSnapshotAsync(object snapshot)
        {
            var index = Index;
            await _state.PersistSnapshotAsync(ActorId, index, snapshot);
            _context.Self.Tell(new PersistedSnapshot(index, snapshot));
        }

        public async Task DeleteSnapshotsAsync(long inclusiveToIndex)
        {
            await _state.DeleteSnapshotsAsync(ActorId, inclusiveToIndex);
        }

        public async Task DeleteEventsAsync(long inclusiveToIndex)
        {
            await _state.DeleteEventsAsync(ActorId, inclusiveToIndex);
        }

        public static Func<Receive, Receive> Using(IProvider provider)
        {
            return next => async context =>
            {
                if (!_initialized)
                {
                    _initialized = true;

                    switch (context.Message)
                    {
                        case Started _:
                            if (context.Actor is IPersistentActor actor)
                            {
                                actor.Persistence = new Persistence();
                                await actor.Persistence.InitAsync(provider, context);
                            }
                            break;
                    }
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