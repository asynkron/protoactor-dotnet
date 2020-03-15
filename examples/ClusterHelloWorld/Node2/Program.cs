// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.SingleRemoteInstance;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var log = LoggerFactory.Create(x => x.AddConsole().SetMinimumLevel(LogLevel.Debug));
            Log.SetLoggerFactory(log);
            Console.WriteLine("Starting Node2");

            var system = new ActorSystem();
            var serialization = new Serialization();
            var context = new RootContext(system);
            serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            var Cluster = new Cluster(system, serialization);

            var props = Props.FromFunc(
                ctx =>
                {
                    switch (ctx.Message)
                    {
                        case HelloRequest _:
                            ctx.Respond(new HelloResponse { Message = "Hello from node 2" });
                            break;
                    }

                    return Actor.Done;
                }
            );

            var parsedArgs = ParseArgs(args);
            Cluster.Remote.RegisterKnownKind("HelloKind", props);

            // SINGLE REMOTE INSTANCE
            // Cluster.Start("MyCluster", parsedArgs.ServerName, 12000, new SingleRemoteInstanceProvider(parsedArgs.ServerName, 12001));

            // CONSUL 
            await Cluster.Start(
                "MyCluster", "node2", 12000, new ConsulProvider(new ConsulProviderOptions(), c => c.Address = new Uri("http://consul:8500/"))
            );

            await Task.Delay(-1);

            Console.WriteLine("Shutting Down...");
            await Cluster.Shutdown();
        }

        private static Node2Config ParseArgs(string[] args)
            => args.Length > 0 ? new Node2Config(args[0], args[1]) : new Node2Config("localhost", "localhost");

        class Node2Config
        {
            public string ServerName { get; }
            public string ConsulUrl { get; }

            public Node2Config(string serverName, string consulUrl)
            {
                ServerName = serverName;
                ConsulUrl = consulUrl;
            }
        }
    }
}