// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
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

internal class Program
{
    private static async Task Main(string[] args)
    {
        Log.SetLoggerFactory(LoggerFactory.Create(c => c
                .SetMinimumLevel(LogLevel.Information)
                .AddConsole()
            )
        );

        ILogger logger = Log.CreateLogger<Program>();
#if NETCORE
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif

        Console.WriteLine("Enter 0 to use GrpcCore provider");
        Console.WriteLine("Enter 1 to use GrpcNet provider");
        if (!int.TryParse(Console.ReadLine(), out int provider))
        {
            provider = 0;
        }

        Console.WriteLine("Enter client advertised host (Enter = localhost)");
        string advertisedHost = Console.ReadLine().Trim();
        if (advertisedHost == "")
        {
            advertisedHost = "127.0.0.1";
        }

        Console.WriteLine("Enter remote advertised host (Enter = localhost)");
        string remoteAddress = Console.ReadLine().Trim();

        if (remoteAddress == "")
        {
            remoteAddress = "127.0.0.1";
        }

        ActorSystemConfig actorSystemConfig = new ActorSystemConfig()
            .WithDeadLetterThrottleCount(10)
            .WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(2));
        ActorSystem system = new(actorSystemConfig);
        RootContext context = new(system);

        IRemote remote;

        if (provider == 0)
        {
            GrpcCoreRemoteConfig remoteConfig = GrpcCoreRemoteConfig
                .BindTo(advertisedHost)
                .WithProtoMessages(ProtosReflection.Descriptor);
            remote = new GrpcCoreRemote(system, remoteConfig);
        }
        else
        {
            GrpcNetRemoteConfig remoteConfig = GrpcNetRemoteConfig
                .BindTo(advertisedHost)
                .WithChannelOptions(new GrpcChannelOptions
                    {
                        CompressionProviders = new[] {new GzipCompressionProvider(CompressionLevel.Fastest)}
                    }
                )
                .WithProtoMessages(ProtosReflection.Descriptor);
            remote = new GrpcNetRemote(system, remoteConfig);
        }

        await remote.StartAsync();

        int messageCount = 1000000;
        CancellationTokenSource cancellationTokenSource = new();
        _ = SafeTask.Run(async () =>
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    SemaphoreSlim semaphore = new(0);
                    Props props = Props.FromProducer(() => new LocalActor(0, messageCount, semaphore));

                    PID pid = context.Spawn(props);

                    try
                    {
                        ActorPidResponse actorPidResponse =
                            await remote.SpawnAsync($"{remoteAddress}:12000", "echo", TimeSpan.FromSeconds(1));

                        if (actorPidResponse.StatusCode == (int)ResponseStatusCode.OK)
                        {
                            PID remotePid = actorPidResponse.Pid;
                            await context.RequestAsync<Start>(remotePid, new StartRemote {Sender = pid},
                                TimeSpan.FromSeconds(1)
                            );
                            Stopwatch stopWatch = new();
                            stopWatch.Start();
                            Console.WriteLine("Starting to send");
                            Ping msg = new();

                            for (int i = 0; i < messageCount; i++)
                            {
                                context.Send(remotePid, msg);
                            }

                            CancellationTokenSource linkedTokenSource =
                                CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token,
                                    new CancellationTokenSource(2000).Token
                                );
                            await semaphore.WaitAsync(linkedTokenSource.Token);
                            stopWatch.Stop();
                            TimeSpan elapsed = stopWatch.Elapsed;
                            Console.WriteLine("Elapsed {0}", elapsed);

                            double t = messageCount * 2.0 / elapsed.TotalMilliseconds * 1000;
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
                    _count++;
                    if (_count % 50000 == 0)
                    {
                        Console.WriteLine(_count);
                    }

                    if (_count == _messageCount)
                    {
                        _semaphore.Release();
                    }

                    break;
            }

            return Task.CompletedTask;
        }
    }
}
