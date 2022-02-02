// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Net.Compression;
using Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    private static async Task Main()
    {
        Log.SetLoggerFactory(LoggerFactory.Create(c => c
                .SetMinimumLevel(LogLevel.Information)
                .AddFilter("Microsoft", LogLevel.None)
                .AddFilter("Grpc", LogLevel.None)
                .AddConsole()
            )
        );

        var logger = Log.CreateLogger<Program>();
#if NETCORE
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif

        Console.WriteLine("Enter 0 to use GrpcNet provider (Default)");
        Console.WriteLine("Enter 1 to use GrpcCore provider");
        if (!int.TryParse(Console.ReadLine(), out var provider))
            provider = 0;

        var serverRemote = 0;
        if (provider == 0)
        {
            Console.WriteLine("Enter 0 to use Server to Server communication (Default)");
            Console.WriteLine("Enter 1 to use Client to Remote communication");
            if (!int.TryParse(Console.ReadLine(), out serverRemote))
                serverRemote = 0;
        }

        var advertisedHost = "";

        if (serverRemote == 0)
        {
            Console.WriteLine("Enter client advertised host (Default = 127.0.0.1)");
            advertisedHost = Console.ReadLine().Trim();
            if (string.IsNullOrEmpty(advertisedHost))
                advertisedHost = "127.0.0.1";
        }

        Console.WriteLine("Enter remote advertised host (Default = 127.0.0.1)");
        var remoteAddress = Console.ReadLine().Trim();

        if (string.IsNullOrEmpty(remoteAddress)) remoteAddress = "127.0.0.1";

        var actorSystemConfig = new ActorSystemConfig()
            .WithDeadLetterThrottleCount(10)
            .WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(2));
        var system = new ActorSystem(actorSystemConfig);
        var context = new RootContext(system);

        IRemote remote;

        if (provider == 0)
        {
            var remoteConfig = GrpcNetRemoteConfig
               .BindTo(advertisedHost)
               .WithChannelOptions(new GrpcChannelOptions
               {
                   CompressionProviders = new[]
                       {
                            new GzipCompressionProvider(CompressionLevel.Fastest)
                       }
               }
               )
               .WithEndpointWriterMaxRetries(3)
               .WithProtoMessages(ProtosReflection.Descriptor);
            if (serverRemote == 0)
                remote = new GrpcNetRemote(system, remoteConfig);
            else
                remote = new GrpcNetClientRemote(system, remoteConfig);
        }
        else
        {
            var remoteConfig = GrpcCoreRemoteConfig
                .BindTo(advertisedHost)
                .WithProtoMessages(ProtosReflection.Descriptor);
            remote = new GrpcCoreRemote(system, remoteConfig);
        }

        await remote.StartAsync();

        var msg = new Ping();

        var messageCount = 1_000_000;
        var cancellationTokenSource = new CancellationTokenSource();
        _ = SafeTask.Run(async () => {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var semaphore = new SemaphoreSlim(0);
                var props = Props.FromProducer(() => new LocalActor(0, messageCount, semaphore));

                var pid = context.Spawn(props);

                try
                {
                    var actorPidResponse =
                        await remote.SpawnAsync($"{remoteAddress}:12000", "echo", TimeSpan.FromSeconds(1));

                    if (actorPidResponse.StatusCode == (int) ResponseStatusCode.OK)
                    {
                        var remotePid = actorPidResponse.Pid;
                        await context.RequestAsync<Start>(remotePid, new StartRemote { Sender = pid },
                            TimeSpan.FromSeconds(1)
                        );
                        var stopWatch = new Stopwatch();
                        stopWatch.Start();
                        Console.WriteLine("Starting to send");


                        for (var i = 0; i < messageCount; i++)
                        {
                            context.Send(remotePid, msg);
                        }

                        var linkedTokenSource =
                            CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token,
                                new CancellationTokenSource(5000).Token
                            );
                        await semaphore.WaitAsync(linkedTokenSource.Token);
                        stopWatch.Stop();
                        var elapsed = stopWatch.Elapsed;
                        Console.WriteLine("Elapsed {0}", elapsed);

                        var t = messageCount * 2.0 / elapsed.TotalMilliseconds * 1000;
                        Console.Clear();
                        Console.WriteLine("Throughput {0} msg / sec", t);
                        await context.StopAsync(remotePid);
                    }
                }
                catch (OperationCanceledException)
                {
                    await Task.Delay(1000);
                }
                catch (Exception e)
                {
                    logger?.LogError(e, "Error");
                    await Task.Delay(5000);
                }

                await context.PoisonAsync(pid);
            }
        }, cancellationTokenSource.Token
        );

        Console.ReadLine();
        cancellationTokenSource.Cancel();
        await Task.Delay(1000);
        Console.WriteLine("Press enter to quit");
        Console.ReadLine();
        await remote.ShutdownAsync();
    }

    public class LocalActor : IActor
    {
        private readonly int _messageCount;
        private readonly SemaphoreSlim _semaphore;
        private int _count;

        public LocalActor(int count, int messageCount, SemaphoreSlim semaphore)
        {
            _count = count;
            _messageCount = messageCount;
            _semaphore = semaphore;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Pong _:
                    if (++_count % 50000 == 0)
                    {
                        // Console.WriteLine(_count);
                    }
                    if (_count == _messageCount) _semaphore.Release();
                    break;
            }

            return Task.CompletedTask;
        }
    }
}