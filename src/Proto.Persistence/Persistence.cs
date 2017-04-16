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

        public async Task InitAsync(IProvider provider, IContext context)
        {
            _state = provider.GetState();
            _context = context;

            await _context.ReceiveAsync(new RecoveryStarted());

            var (snapshot, index) = await _state.GetSnapshotAsync(ActorId);

            if (snapshot != null)
            {
                Index = index;
                await OnRecoverSnapshot(this, new RecoverSnapshotArgs(snapshot));
            };

            await _state.GetEventsAsync(ActorId, Index, async @event =>
            {
                await OnRecoverEvent(this, new RecoverEventArgs(@event));
                Index++;
            });
            
            await _context.ReceiveAsync(new RecoveryCompleted());
        }

        public async Task PersistEventAsync(object @event)
        {
            var index = Index;
            await _state.PersistEventAsync(ActorId, index, @event);
            Index++;
            await OnPersistedEvent(this, new PersistedEventArgs(index, @event));
        }

        public async Task PersistSnapshotAsync(object snapshot)
        {
            var index = Index;
            await _state.PersistSnapshotAsync(ActorId, index, snapshot);
            await OnPersistedSnapshot(this, new PersistedSnapshotArgs(index, snapshot));
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

                await next(context);
            };
        }

        public delegate Task RecoverSnapshotHandler(object sender, RecoverSnapshotArgs e);

        public delegate Task RecoverEventHandler(object sender, RecoverEventArgs e);

        public delegate Task PersistedEventHandler(object sender, PersistedEventArgs e);

        public delegate Task PersistedSnapshotHandler(object sender, PersistedSnapshotArgs e);

        public event RecoverSnapshotHandler OnRecoverSnapshot;

        public event RecoverEventHandler OnRecoverEvent;

        public event PersistedEventHandler OnPersistedEvent;

        public event PersistedSnapshotHandler OnPersistedSnapshot;
    }

    public class RecoverSnapshotArgs : EventArgs
    {
        public RecoverSnapshotArgs(object snapshot)
        {
            Snapshot = snapshot;
        }

        public object Snapshot { get; }
    }

    public class RecoverEventArgs : EventArgs
    {
        public RecoverEventArgs(object @event)
        {
            Event = @event;
        }

        public object Event { get; }
    }

    public class PersistedEventArgs : EventArgs
    {
        public PersistedEventArgs(long index, object @event)
        {
            Index = index;
            Event = @event;
        }

        public long Index { get; }
        public object Event { get; }
    }

    public class PersistedSnapshotArgs : EventArgs
    {
        public PersistedSnapshotArgs(long index, object snapshot)
        {
            Index = index;
            Snapshot = snapshot;
        }

        public long Index { get; }
        public object Snapshot { get; }
    }

    public class RequestSnapshot { }
    public class RecoveryStarted { }
    public class RecoveryCompleted { }
}