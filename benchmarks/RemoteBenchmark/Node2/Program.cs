// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    public class EchoActor : IActor
    {
        private PID _sender;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case StartRemote sr:
                    // Console.WriteLine("Starting");
                    _sender = sr.Sender;
                    context.Respond(new Start());
                    return Actor.Done;
                case Ping _:
                    context.Send(_sender, new Pong());
                    return Actor.Done;
                default:
                    return Actor.Done;
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Log.SetLoggerFactory(LoggerFactory.Create(b => b.AddConsole()
                                                            .AddFilter("Proto.EventStream", LogLevel.Critical)
                                                            .AddFilter("Microsoft", LogLevel.Error)
                                                            .AddFilter("Grpc.AspNetCore", LogLevel.Error)
                                                            .SetMinimumLevel(LogLevel.Information)));
            var system = new ActorSystem();
            var Remote = new SelfHostedRemote(system, 12000, remote =>
            {
                remote.Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            });
            Remote.Start();
            system.Root.SpawnNamed(Props.FromProducer(() => new EchoActor()), "remote");
            Console.ReadLine();
            await Remote.ShutdownAsync();
        }
    }
}