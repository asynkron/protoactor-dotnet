using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;

namespace ClusterExperiment1
{
    public static class Program
    {
        private static async Task RunFollower()
        {
            Log.SetLoggerFactory(LoggerFactory.Create(l => l.AddConsole(o =>
                        {
                            o.IncludeScopes = false;
                            o.TimestampFormat = "hh:mm:ss:fff - ";
                        }
                    )
                    // .AddFilter("Microsoft.AspNetCore", LogLevel.Warning)
                    // .AddFilter("Grpc.AspNetCore.Server.ServerCallHandler", LogLevel.Critical)
                    .SetMinimumLevel(LogLevel.Information)
                )
            );

            var cluster = SpawnMember(0);

            Console.ReadLine();
            await cluster.ShutdownAsync();
        }
        private static async Task RunLeader()
        {
            Log.SetLoggerFactory(LoggerFactory.Create(l => l.AddConsole(o =>
            {
                o.IncludeScopes = false;
                o.TimestampFormat = "hh:mm:ss:fff - ";
            }
                    )
                    // .AddFilter("Microsoft.AspNetCore", LogLevel.Warning)
                    // .AddFilter("Grpc.AspNetCore.Server.ServerCallHandler", LogLevel.Critical)
                    .SetMinimumLevel(LogLevel.Information)
                )
            );
            var logger = Log.CreateLogger(nameof(Program));

            Console.WriteLine("Press enter to start");
            Console.WriteLine();
            Console.WriteLine("Red = spawned grains");
            Console.WriteLine("Yellow = cluster topology events");
            Console.WriteLine("Each '.' is a request/response call to one of the grains");
            Console.WriteLine("Enter spawns a new node in the cluster");
            Console.ReadLine();
            var system = new ActorSystem();
            var consulProvider = new ConsulProvider(new ConsulProviderOptions());
            var remote = system.AddRemote(8090, remote =>
            {
                remote.Serialization.RegisterFileDescriptor(MessagesReflection.Descriptor);
                remote.RemoteConfig.UseHttps = true;
                remote.RemoteConfig.ConfigureKestrel = ConfigureListenOptions;
                remote.RemoteConfig.ChannelOptions.HttpClient = GetConfiguredHttpClient();
            });
            var c1 = new Cluster(system, new ClusterConfig("mycluster", consulProvider).WithPidCache(false));
            await c1.StartAsync();

            _ = Task.Run(async () =>
            {
                var rnd = new Random();
                while (true)
                {
                    try
                    {
                        var id = "myactor" + rnd.Next(0, 1000);
                        var res = await c1.RequestAsync<HelloResponse>(id, "hello", new HelloRequest(),
                            new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token
                        );

                        if (res == null)
                        {
                            logger.LogError("Null response");
                        }
                        else
                        {
                            Console.Write(".");
                        }
                    }
                    catch (Exception)
                    {
                        logger.LogError("Request timeout");
                    }
                }
            }
            );


            Console.ReadLine();
            await c1.ShutdownAsync();
        }
        private static void ConfigureListenOptions(ListenOptions listenOptions)
        {
            var serverCert = new X509Certificate2(Path.Combine(Environment.CurrentDirectory, "server.pfx"), "1111");
            listenOptions.UseHttps(serverCert, c =>
            {
                c.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                c.ClientCertificateValidation = (cert, chain, policy) => cert.Issuer == serverCert.Issuer;
            });
        }
        private static HttpClient GetConfiguredHttpClient()
        {
            var cert = new X509Certificate2(Path.Combine(Environment.CurrentDirectory, "client.pfx"), "1111");
            var handler = new HttpClientHandler();

            handler.ServerCertificateCustomValidationCallback = (req, cert, chain, policy) => true;
            handler.ClientCertificates.Add(cert);
            var httpClient = new HttpClient(handler);
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            return httpClient;
        }
        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                await RunLeader();
            }
            else
            {
                await RunFollower();
            }
        }
        private static Cluster SpawnMember(int port)
        {
            var helloProps = Props.FromProducer(() => new HelloActor());
            var system = new ActorSystem();
            var consulProvider = new ConsulProvider(new ConsulProviderOptions());
            ClusterConfig clusterConfig = new ClusterConfig("mycluster", consulProvider).WithPidCache(false);
            var remote = new SelfHostedRemote(system, port, remote =>
            {
                remote.Serialization.RegisterFileDescriptor(MessagesReflection.Descriptor);
                remote.RemoteKindRegistry.RegisterKnownKind("hello", helloProps);
                remote.RemoteConfig.UseHttps = true;
                remote.RemoteConfig.ConfigureKestrel = ConfigureListenOptions;
                remote.RemoteConfig.ChannelOptions.HttpClient = GetConfiguredHttpClient();
            });
            var clusterNode = new Cluster(system, clusterConfig);
            _ = clusterNode.StartAsync();
            return clusterNode;
        }
    }

    public class HelloActor : IActor
    {
        //   private readonly ILogger _log = Log.CreateLogger<HelloActor>();

        public Task ReceiveAsync(IContext ctx)
        {
            if (ctx.Message is Started)
            {
                //just to highlight when this happens
                Console.Write("#");
                //_log.LogInformation("I started " + ctx.Self);
            }

            if (ctx.Message is HelloRequest)
            {
                ctx.Respond(new HelloResponse());
            }

            if (ctx.Message is Stopped)
            {
                //just to highlight when this happens
                //    _log.LogWarning("I stopped" + ctx.Self);
            }

            return Actor.Done;
        }
    }
}