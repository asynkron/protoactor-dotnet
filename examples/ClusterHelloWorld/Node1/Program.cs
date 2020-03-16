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
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace Node1
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var log = LoggerFactory.Create(x => x.AddConsole().SetMinimumLevel(LogLevel.Debug));
            Log.SetLoggerFactory(log);

            Console.WriteLine("Starting Node1");
            var system = new ActorSystem();
            var serialization = new Serialization();
            var context = new RootContext(system);
            serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            var Cluster = new Cluster(system, serialization);
            var parsedArgs = ParseArgs(args);
            // SINGLE REMOTE INSTANCE
            // Cluster.Start("MyCluster", parsedArgs.ServerName, 12001, new SingleRemoteInstanceProvider("localhost", 12000));

            // CONSUL 

            await Cluster.Start(
                "MyCluster", "node1", 12001, new ConsulProvider(new ConsulProviderOptions(), c => c.Address = new Uri("http://consul:8500/"))
            );

            var (pid, sc) = await Cluster.GetAsync("TheName", "HelloKind");

            while (sc != ResponseStatusCode.OK)
                (pid, sc) = await Cluster.GetAsync("TheName", "HelloKind");

            var i = 10000;
            while (i-- > 0)
            {
                var res = await context.RequestAsync<HelloResponse>(pid, new HelloRequest());
                Console.WriteLine(res.Message);
                await Task.Delay(500);
            }

            await Task.Delay(-1);
            Console.WriteLine("Shutting Down...");

            await Cluster.Shutdown();
        }

        private static Node1Config ParseArgs(string[] args)
            => args.Length > 0 ? new Node1Config(args[0], args[1], bool.Parse(args[2])) : new Node1Config("localhost", "localhost", true);

        class Node1Config
        {
            public string ServerName { get; }
            public string ConsulUrl { get; }
            public bool StartConsul { get; }

            public Node1Config(string serverName, string consulUrl, bool startConsul)
            {
                ServerName = serverName;
                ConsulUrl = consulUrl;
                StartConsul = startConsul;
            }
        }
    }
}