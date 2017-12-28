// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    static void Main(string[] args)
    {
        Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        var parsedArgs = parseArgs(args);
        if(parsedArgs.StartConsul)
        {
            StartConsulDevMode();
        }
        Cluster.Start("MyCluster", parsedArgs.ServerName, 12001, new ConsulProvider(new ConsulProviderOptions(), c => c.Address = new Uri("http://" + parsedArgs.ConsulUrl + ":8500/")));
        var (pid, sc) = Cluster.GetAsync("TheName", "HelloKind").Result;
        while (sc != ResponseStatusCode.OK)
            (pid, sc) = Cluster.GetAsync("TheName", "HelloKind").Result;
        var res = pid.RequestAsync<HelloResponse>(new HelloRequest()).Result;
        Console.WriteLine(res.Message);
        Thread.Sleep(System.Threading.Timeout.Infinite);
        Console.WriteLine("Shutting Down...");
        Cluster.Shutdown();
    }

    private static void StartConsulDevMode()
    {
        Console.WriteLine("Consul - Starting");
        ProcessStartInfo psi =
            new ProcessStartInfo(@"..\..\..\dependencies\consul",
                "agent -server -bootstrap -data-dir /tmp/consul -bind=127.0.0.1 -ui")
            {
                CreateNoWindow = true,
            };
        Process.Start(psi);
        Console.WriteLine("Consul - Started");
    }

    private static Node1Config parseArgs(string[] args)
    {
        if(args.Length > 0) 
        {
            return new Node1Config(args[0], args[1], bool.Parse(args[2]));
        }
        return new Node1Config("127.0.0.1", "127.0.0.1", true);
    }

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