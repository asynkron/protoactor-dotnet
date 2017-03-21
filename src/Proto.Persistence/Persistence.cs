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
        public ulong EventIndex { get; set; }
        public IContext Context { get; set; }
        public string Name => Context.Self.Id;

        public async Task InitAsync(IProvider provider, IContext context)
        {
            State = provider.GetState();
            Context = context;

            await Context.ReceiveAsync(new RecoveryStarted());

            var t = await State.GetSnapshotAsync(Name);

            if (t != null)
            {
                EventIndex = t.Item2;
                await Context.ReceiveAsync(new RecoverSnapshot(t.Item1));
            }

            await State.GetEventsAsync(Name, EventIndex, e =>
            {
                Context.ReceiveAsync(new RecoverEvent(e)).Wait();
                EventIndex++;
            });
            
            await Context.ReceiveAsync(new RecoveryCompleted());
        }

        public async Task PersistReceiveAsync(object message)
        {
            var persistEventIndex = EventIndex;

            await State.PersistEventAsync(Name, persistEventIndex, message);

            EventIndex++;

            await Context.ReceiveAsync(new PersistedEvent(persistEventIndex, message));
        }

        public async Task PersistSnapshotAsync(object snapshot)
        {
            await State.PersistSnapshotAsync(Name, EventIndex, snapshot);
        }

        public static Func<Receive, Receive> Using(IProvider provider)
        {
            return next => async context =>
            {
                switch (context.Message)
                {
                    case Started _:
                        var p = context.Actor as IPersistentActor;
                        if (p != null)
                        {
                            p.Persistence = new Persistence();
                            await p.Persistence.InitAsync(provider, context);
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
        public RecoverEvent(object message)
        {
            Message = message;
        }
        
        public object Message { get; }
    }

    public class RecoveryStarted { }
    public class RecoveryCompleted { }

    public class PersistedEvent
    {
        public PersistedEvent(ulong eventIndex, object message)
        {
            EventIndex = eventIndex;
            Message = message;
        }

        public ulong EventIndex { get; }
        public object Message { get; }
    }
}