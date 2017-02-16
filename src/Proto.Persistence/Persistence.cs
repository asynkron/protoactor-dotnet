// -----------------------------------------------------------------------
//  <copyright file="Persistence.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Proto.Persistence
{
    public class Persistence
    {
        public IProviderState State { get; set; }
        public int EventIndex { get; set; }
        public IContext Context { get; set; }
        public bool Recovering { get; set; }
        public string Name => Context.Self.Id;

        public void Init(IProvider provider, IContext context)
        {
            State = provider.GetState();
            Context = context;
            Recovering = true;

            State.Restart();
            var t = State.GetSnapshot(Name);
            if (t != null)
            {
                EventIndex = t.Item2;
                Context.ReceiveAsync(t.Item1).Wait();
            }
            State.GetEvents(Name, EventIndex, e =>
            {
                Context.ReceiveAsync(e).Wait();
                EventIndex++;
            });
        }

        public async Task PersistReceiveAsync(IMessage message)
        {
            State.PersistEventAsync(Name, EventIndex, message);
            EventIndex++;
            await Context.ReceiveAsync(message);
            if (State.GetSnapshotInterval() == 0)
            {
                await Context.ReceiveAsync(new RequestSnapshot());
            }
        }

        public async Task PersistSnapshotAsync(IMessage snapshot)
        {
            await State.PersistSnapshotAsync(Name, EventIndex, snapshot);
        }

        public static Func<Receive, Receive> Using(IProvider provider)
        {
            return next => async context =>
            {
                switch (context.Message)
                {
                    case Started started:
                    case Replay replay:
                        var p = context.Actor as IPersistentActor;
                        if (p != null)
                        {
                            p.Persistence = new Persistence();
                            p.Persistence.Init(provider, context);
                        }
                        break;
                    default:
                        await next(context);
                        break;
                }
            };
        }
    }

    public class RequestSnapshot
    {
    }
}