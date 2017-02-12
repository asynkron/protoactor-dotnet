// -----------------------------------------------------------------------
//  <copyright file="Persistence.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
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

        public void PersistReceive(IMessage message)
        {
            State.PersistEvent(Name, EventIndex, message);
            EventIndex++;
            Context.ReceiveAsync(message).Wait();
            if (State.GetSnapshotInterval() == 0)
            {
                Context.ReceiveAsync(new RequestSnapshot()).Wait();
            }
        }

        public void PersistSnapshot(IMessage snapshot)
        {
            State.PersistSnapshot(Name, EventIndex, snapshot);
        }

        public static Func<Receive, Receive> Using(IProvider provider)
        {
            return next => async context =>
            {
                switch (context.Message)
                {
                    case Started started:
                        await next(context);
                        context.Self.Tell(new Replay());
                        break;
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