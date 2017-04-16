// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Persistence;
using Proto.Persistence.Sqlite;

class Program
{
    static void Main(string[] args)
    {
        var provider = new SqliteProvider();

        var props = Actor.FromProducer(() => new MyPersistenceActor())
            .WithReceiveMiddleware(Persistence.Using(provider));

        var pid = Actor.Spawn(props);

        Console.ReadLine();
    }

    class MyPersistenceActor : IPersistentActor
    {
        private PID _loopActor;
        private State _state = new State();
        public Persistence Persistence { get; set; }
        private class StartLoopActor { }
        private class TimeToSnapshot { }

        private bool _timerStarted = false;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started msg:

                    RegisterHandlers();

                    Console.WriteLine("MyPersistenceActor - Started");

                    context.Self.Tell(new StartLoopActor());

                    break;
                case RecoveryStarted msg:

                    RegisterHandlers();

                    Console.WriteLine("MyPersistenceActor - RecoveryStarted");

                    break;
                case RecoveryCompleted msg:

                    Console.WriteLine("MyPersistenceActor - RecoveryCompleted");

                    context.Self.Tell(new StartLoopActor());

                    break;
                case RequestSnapshot msg:

                    await Handle(context, msg);

                    break;
                case TimeToSnapshot msg:

                    await Handle(context, msg);

                    break;
                case StartLoopActor msg:

                    await Handle(context, msg);

                    break;
                case RenameCommand msg:

                    await Handle(msg);

                    break;
            }
        }

        private void RegisterHandlers()
        {
            Persistence.OnRecoverSnapshot += Persistence_OnRecoverSnapshot;
            Persistence.OnRecoverEvent += Persistence_OnRecoverEvent;
            Persistence.OnPersistedSnapshot += Persistence_OnPersistedSnapshot;
            Persistence.OnPersistedEvent += Persistence_OnPersistedEvent;
        }

        private Task Persistence_OnRecoverSnapshot(object sender, RecoverSnapshotArgs e)
        {
            if (e.Snapshot is State ss)
            {
                _state = ss;

                Console.WriteLine("MyPersistenceActor - RecoverSnapshot = {0}, Snapshot.Name = {1}", Persistence.Index, ss.Name);
            }

            return Actor.Done;
        }

        private Task Persistence_OnRecoverEvent(object sender, RecoverEventArgs e)
        {
            if (e.Event is RenameEvent recev)
            {
                Console.WriteLine("MyPersistenceActor - RecoverEvent = {0}, Event.Name = {1}", Persistence.Index, recev.Name);
            }

            return Actor.Done;
        }

        private async Task Persistence_OnPersistedSnapshot(object sender, PersistedSnapshotArgs e)
        {
            Console.WriteLine("MyPersistenceActor - PersistedSnapshot at Index = {0}", e.Index);

            await Persistence.DeleteSnapshotsAsync(e.Index - 1);
        }

        private Task Persistence_OnPersistedEvent(object sender, PersistedEventArgs e)
        {
            Console.WriteLine("MyPersistenceActor - PersistedEvent = {0}", e.Index);

            if (e.Event is RenameEvent rne)
            {
                _state.Name = rne.Name;
            }

            return Actor.Done;
        }

        private async Task Handle(IContext context, RequestSnapshot message)
        {
            Console.WriteLine("MyPersistenceActor - RequestSnapshot");

            await Persistence.PersistSnapshotAsync(_state);

            context.Self.Tell(new TimeToSnapshot());
        }

        private Task Handle(IContext context, TimeToSnapshot message)
        {
            Console.WriteLine("MyPersistenceActor - TimeToSnapshot");

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));

                context.Self.Tell(new RequestSnapshot());
            });

            return Actor.Done;
        }

        private Task Handle(IContext context, StartLoopActor message)
        {
            if (_timerStarted) return Actor.Done;

            _timerStarted = true;

            Console.WriteLine("MyPersistenceActor - StartLoopActor");

            var props = Actor.FromProducer(() => new LoopActor());

            _loopActor = context.Spawn(props);

            context.Self.Tell(new TimeToSnapshot());
            
            return Actor.Done;
        }

        private async Task Handle(RenameCommand message)
        {
            Console.WriteLine("MyPersistenceActor - RenameCommand");

            await Persistence.PersistEventAsync(new RenameEvent { Name = message.Name });
        }
    }

    class LoopActor : IActor
    {
        internal class LoopParentMessage { }

        public Task ReceiveAsync(IContext context)
        {
            switch(context.Message)
            {
                case Started _:

                    Console.WriteLine("LoopActor - Started");

                    context.Self.Tell(new LoopParentMessage());

                    break;
                case LoopParentMessage msg:

                    Task.Run(async () => {
                        
                        context.Parent.Tell(new RenameCommand { Name = "Daniel" });

                        await Task.Delay(TimeSpan.FromSeconds(2));

                        context.Self.Tell(new LoopParentMessage());
                    });

                    break;
            }

            return Actor.Done;
        }
    }
}