// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using Messages;
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
        static void Main(string[] args)
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case HelloRequest _:
                        ctx.Respond(new HelloResponse
                        {
                            Message = "Hello from node 2"
                        });
                        break;
                }
                return Actor.Done;
            });

            var parsedArgs = ParseArgs(args);
            Remote.RegisterKnownKind("HelloKind", props);

            // SINGLE REMOTE INSTANCE
            Cluster.Start("MyCluster", parsedArgs.ServerName, 12000, new SingleRemoteInstanceProvider(parsedArgs.ServerName, 12000));

            // CONSUL 
            //Cluster.Start("MyCluster", parsedArgs.ServerName, 12000, new ConsulProvider(new ConsulProviderOptions(), c => c.Address = new Uri("http://" + parsedArgs.ConsulUrl + ":8500/")));

            Thread.Sleep(Timeout.Infinite);
            Console.WriteLine("Shutting Down...");
            Cluster.Shutdown();
        }

        private static Node2Config ParseArgs(string[] args)
        {
            if(args.Length > 0) 
            {
                return new Node2Config(args[0], args[1]);
            }
            return new Node2Config("127.0.0.1", "127.0.0.1");
        }

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