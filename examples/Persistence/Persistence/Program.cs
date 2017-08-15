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
using Event = Proto.Persistence.Event;
using Snapshot = Proto.Persistence.Snapshot;
using System.Text;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main(string[] args)
    {
        var provider = new SqliteProvider(new SqliteConnectionStringBuilder { DataSource = "states.db" });

        var props = Actor.FromProducer(() => new MyPersistenceActor(provider));

        var pid = Actor.Spawn(props);

        Console.ReadLine();
    }

    public class RequestSnapshot { }

    class MyPersistenceActor : IActor
    {
        private PID _loopActor;
        private State _state = new State();
        private readonly Persistence _persistence;

        public MyPersistenceActor(IProvider provider)
        {
            _persistence = Persistence.WithEventSourcingAndSnapshotting(provider, provider, "demo-app-id", Apply, Apply);
        }

        private void Apply(Event @event)
        {
            switch (@event)
            {
                case RecoverEvent msg:
                    if(msg.Data is RenameEvent re)
                    {
                        _state.Name = re.Name;
                        Console.WriteLine("MyPersistenceActor - RecoverEvent = Event.Index = {0}, Event.Data = {1}", msg.Index, msg.Data);
                    }
                    break;
                case PersistedEvent msg:
                    Console.WriteLine("MyPersistenceActor - PersistedEvent = Event.Index = {0}, Event.Data = {1}", msg.Index, msg.Data);
                    break;
            }
        }

        private void Apply(Snapshot snapshot)
        {
            switch (snapshot)
            {
                case RecoverSnapshot msg:
                    if (msg.State is State ss)
                    {
                        _state = ss;
                        Console.WriteLine("MyPersistenceActor - RecoverSnapshot = Snapshot.Index = {0}, Snapshot.State = {1}", _persistence.Index, ss.Name);
                    }
                    break;
            }
        }

        private class StartLoopActor { }
        private class TimeToSnapshot { }

        private bool _timerStarted = false;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started msg:
                    
                    Console.WriteLine("MyPersistenceActor - Started");

                    Console.WriteLine("MyPersistenceActor - Current State: {0}", _state);

                    await _persistence.RecoverStateAsync();

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

        private async Task Handle(IContext context, RequestSnapshot message)
        {
            Console.WriteLine("MyPersistenceActor - RequestSnapshot");

            await _persistence.PersistSnapshotAsync(_state);
            Console.WriteLine("MyPersistenceActor - PersistedSnapshot = Snapshot.Index = {0}, Snapshot.State = {1}", _persistence.Index, _state);
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

            _state.Name = message.Name;

            await _persistence.PersistEventAsync(new RenameEvent { Name = message.Name });
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
                        
                        context.Parent.Tell(new RenameCommand { Name = GeneratePronounceableName(5) });

                        await Task.Delay(TimeSpan.FromSeconds(2));

                        context.Self.Tell(new LoopParentMessage());
                    });

                    break;
            }

            return Actor.Done;
        }

        static string GeneratePronounceableName(int length)
        {
            const string vowels = "aeiou";
            const string consonants = "bcdfghjklmnpqrstvwxyz";

            var rnd = new Random();
            var name = new StringBuilder();

            length = length % 2 == 0 ? length : length + 1;

            for (var i = 0; i < length / 2; i++)
            {
                name
                    .Append(vowels[rnd.Next(vowels.Length)])
                    .Append(consonants[rnd.Next(consonants.Length)]);
            }

            return name.ToString();
        }
    }
}