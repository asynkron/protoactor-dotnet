﻿// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Text;
using System.Threading.Tasks;
using Messages;
using Microsoft.Data.Sqlite;
using Proto;
using Proto.Persistence;
using Proto.Persistence.SnapshotStrategies;
using Proto.Persistence.Sqlite;
using Event = Proto.Persistence.Event;
using Snapshot = Proto.Persistence.Snapshot;

internal class Program
{
    private static void Main(string[] args)
    {
        RootContext context = new RootContext(new ActorSystem());
        SqliteProvider provider = new SqliteProvider(new SqliteConnectionStringBuilder {DataSource = "states.db"});

        Props props = Props.FromProducer(() => new MyPersistenceActor(provider));

        PID pid = context.Spawn(props);

        Console.ReadLine();
    }

    private class MyPersistenceActor : IActor
    {
        private readonly Persistence _persistence;
        private PID _loopActor;
        private State _state = new State();

        private bool _timerStarted;

        public MyPersistenceActor(IProvider provider) => _persistence = Persistence.WithEventSourcingAndSnapshotting(
            provider,
            provider,
            "demo-app-id",
            ApplyEvent,
            ApplySnapshot,
            new IntervalStrategy(20), () => _state
        );

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started msg:

                    Console.WriteLine("MyPersistenceActor - Started");

                    Console.WriteLine("MyPersistenceActor - Current State: {0}", _state);

                    await _persistence.RecoverStateAsync();

                    context.Send(context.Self, new StartLoopActor());

                    break;

                case StartLoopActor msg:

                    await Handle(context, msg);

                    break;

                case RenameCommand msg:

                    await Handle(msg);

                    break;
            }
        }

        private void ApplyEvent(Event @event)
        {
            switch (@event)
            {
                case RecoverEvent msg:
                    if (msg.Data is RenameEvent re)
                    {
                        _state.Name = re.Name;
                        Console.WriteLine("MyPersistenceActor - RecoverEvent = Event.Index = {0}, Event.Data = {1}",
                            msg.Index, msg.Data);
                    }

                    break;
                case ReplayEvent msg:
                    if (msg.Data is RenameEvent rp)
                    {
                        _state.Name = rp.Name;
                        Console.WriteLine("MyPersistenceActor - ReplayEvent = Event.Index = {0}, Event.Data = {1}",
                            msg.Index, msg.Data);
                    }

                    break;
                case PersistedEvent msg:
                    Console.WriteLine("MyPersistenceActor - PersistedEvent = Event.Index = {0}, Event.Data = {1}",
                        msg.Index, msg.Data);
                    break;
            }
        }

        private void ApplySnapshot(Snapshot snapshot)
        {
            switch (snapshot)
            {
                case RecoverSnapshot msg:
                    if (msg.State is State ss)
                    {
                        _state = ss;
                        Console.WriteLine(
                            "MyPersistenceActor - RecoverSnapshot = Snapshot.Index = {0}, Snapshot.State = {1}",
                            _persistence.Index,
                            ss.Name
                        );
                    }

                    break;
            }
        }

        private Task Handle(IContext context, StartLoopActor message)
        {
            if (_timerStarted)
            {
                return Task.CompletedTask;
            }

            _timerStarted = true;

            Console.WriteLine("MyPersistenceActor - StartLoopActor");

            Props props = Props.FromProducer(() => new LoopActor());

            _loopActor = context.Spawn(props);

            return Task.CompletedTask;
        }

        private async Task Handle(RenameCommand message)
        {
            Console.WriteLine("MyPersistenceActor - RenameCommand");

            _state.Name = message.Name;

            await _persistence.PersistEventAsync(new RenameEvent {Name = message.Name});
        }

        private class StartLoopActor
        {
        }
    }

    private class LoopActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:

                    Console.WriteLine("LoopActor - Started");

                    context.Send(context.Self, new LoopParentMessage());

                    break;
                case LoopParentMessage _:

                    _ = SafeTask.Run(async () =>
                        {
                            context.Send(context.Parent, new RenameCommand {Name = GeneratePronounceableName(5)});

                            await Task.Delay(TimeSpan.FromMilliseconds(500));

                            context.Send(context.Self, new LoopParentMessage());
                        }
                    );

                    break;
            }

            return Task.CompletedTask;
        }

        private static string GeneratePronounceableName(int length)
        {
            const string vowels = "aeiou";
            const string consonants = "bcdfghjklmnpqrstvwxyz";

            Random rnd = new Random();
            StringBuilder name = new StringBuilder();

            length = length % 2 == 0 ? length : length + 1;

            for (int i = 0; i < length / 2; i++)
            {
                name
                    .Append(vowels[rnd.Next(vowels.Length)])
                    .Append(consonants[rnd.Next(consonants.Length)]);
            }

            return name.ToString();
        }

        private class LoopParentMessage
        {
        }
    }
}
