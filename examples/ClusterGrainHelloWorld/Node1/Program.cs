// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    static void Main(string[] args)
    {
        StartConsulDevMode();
        Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

        Cluster.Start("MyCluster", "127.0.0.1", 12001, new ConsulProvider(new ConsulProviderOptions()));

        var client = Grains.HelloGrain("Roger");

        var res = client.SayHello(new HelloRequest()).Result;
        Console.WriteLine(res.Message);
        Console.ReadLine();
        res = client.SayHello(new HelloRequest()).Result;
        Console.WriteLine(res.Message);
        Console.ReadLine();
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
}