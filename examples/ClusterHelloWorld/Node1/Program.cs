// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    static void Main(string[] args)
    {
        Main2().GetAwaiter().GetResult();
    }
    
    public static async Task Main2()
    {
        Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        Remote.Start("127.0.0.1", 12001);
        await Cluster.StartAsync("MyCluster", new ConsulProvider(new ConsulProviderOptions()));
        var pid = Cluster.GetAsync("TheName", "HelloKind").Result;
        var res = await pid.RequestAsync<HelloResponse>(new HelloRequest());
        Console.WriteLine(res.Message);
        Console.ReadLine();
    }
}