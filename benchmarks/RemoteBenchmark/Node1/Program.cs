// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    static readonly PID remoteActor = new PID("127.0.0.1:12000", "remote");

    static async Task Main(string[] args)
    {
        Log.SetLoggerFactory(LoggerFactory.Create(b => b.AddConsole()
                                                            .AddFilter("Proto.EventStream", LogLevel.Critical)
                                                            .AddFilter("Microsoft", LogLevel.Error)
                                                            .AddFilter("Grpc.AspNetCore", LogLevel.Error)
                                                            .SetMinimumLevel(LogLevel.Information)));
        var system = new ActorSystem();
        var Remote = system.AddRemote(12001, remote =>
        {
            remote.Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        });
        Remote.Start();

        var messageCount = 1000000;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                PID pid = null;
                try
                {
                    var wg = new AutoResetEvent(false);
                    var props = Props.FromProducer(() => new LocalActor(0, messageCount, wg));
                    pid = system.Root.Spawn(props);

                    await system.Root.RequestAsync<Start>(remoteActor, new StartRemote { Sender = pid }).ConfigureAwait(false);

                    var start = DateTime.Now;
                    Console.WriteLine("Starting to send");
                    var msg = new Ping();
                    for (var i = 0; i < messageCount; i++)
                    {
                        system.Root.Send(remoteActor, msg);
                    }
                    wg.WaitOne();
                    var elapsed = DateTime.Now - start;
                    Console.Clear();
                    Console.WriteLine("Elapsed {0}", elapsed);

                    var t = messageCount * 2.0 / elapsed.TotalMilliseconds * 1000;
                    Console.WriteLine("Throughput {0} msg / sec", t);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    await Task.Delay(2000);
                    if (pid != null)
                        system.Root.Stop(pid);
                }
            }
        });

        Console.ReadLine();
        await Remote.ShutdownAsync();
    }

    public class LocalActor : IActor
    {
        private int _count;
        private readonly int _messageCount;
        private readonly AutoResetEvent _wg;

        public LocalActor(int count, int messageCount, AutoResetEvent wg)
        {
            _count = count;
            _messageCount = messageCount;
            _wg = wg;
        }


        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Pong _:
                    _count++;
                    if (_count % 50000 == 0)
                    {
                        Console.WriteLine(_count);
                    }
                    if (_count == _messageCount)
                    {
                        _wg.Set();
                    }
                    break;
            }
            return Actor.Done;
        }
    }
}