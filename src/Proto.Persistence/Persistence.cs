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
        public IProviderState State { get; set; }
        public long Index { get; set; }
        public IContext Context { get; set; }
        public string Name => Context.Self.Id;

        public async Task InitAsync(IProvider provider, IContext context)
        {
            State = provider.GetState();
            Context = context;

            await Context.ReceiveAsync(new RecoveryStarted());

            var snapshot = await State.GetSnapshotAsync(Name);
            {
                Index = snapshot.Index;
                await Context.ReceiveAsync(new RecoverSnapshot(snapshot.Data));
            };

            await State.GetEventsAsync(Name, Index, Callback =>
            {
                Context.ReceiveAsync(new RecoverEvent(Callback)).Wait();
                Index++;
            });
            
            await Context.ReceiveAsync(new RecoveryCompleted());
        }

        public async Task PersistEventAsync(object data)
        {
            var index = Index;

            await State.PersistEventAsync(Name, index, data);

            Index++;

            await Context.ReceiveAsync(new PersistedEvent(index, data));
        }

        public async Task PersistSnapshotAsync(object data)
        {
            var index = Index;

            await State.PersistSnapshotAsync(Name, index, data);
            
            await Context.ReceiveAsync(new PersistedSnapshot(index, data));
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
        public RecoverSnapshot(object data)
        {
            Data = data;
        }

        public object Data { get; }
    }

    public class RecoverEvent
    {
        public RecoverEvent(object data)
        {
            Data = data;
        }
        
        public object Data { get; }
    }

    public class RecoveryStarted { }
    public class RecoveryCompleted { }

    public class PersistedEvent
    {
        public PersistedEvent(long index, object data)
        {
            Index = index;
            Data = data;
        }

        public long Index { get; }
        public object Data { get; }
    }

    public class PersistedSnapshot
    {
        public PersistedSnapshot(long index, object data)
        {
            Index = index;
            Data = data;
        }

        public long Index { get; }
        public object Data { get; }
    }
}