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
        Cluster.Start("MyCluster", "node1", 12001, new ConsulProvider(new ConsulProviderOptions(), c => c.Address = new Uri("http://consul:8500/")));
        var (pid, sc) = Cluster.GetAsync("TheName", "HelloKind").Result;
        while (sc != ResponseStatusCode.OK)
            (pid, sc) = Cluster.GetAsync("TheName", "HelloKind").Result;
        var res = pid.RequestAsync<HelloResponse>(new HelloRequest()).Result;
        Console.WriteLine(res.Message);
        System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
        Console.WriteLine("Shutting Down...");
        Cluster.Shutdown();
    }
}