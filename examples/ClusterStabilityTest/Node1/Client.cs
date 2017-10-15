using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace TestApp
{
    public static class Client
    {
        public static void Start()
        {
            var clusterName = "cluster" + DateTime.Now.Ticks;
            StartConsulDevMode();
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            Cluster.Start(clusterName, "127.0.0.1", 0, new ConsulProvider(new ConsulProviderOptions()));

            for (int i = 0; i < 100; i++)
            {
                var psi = new ProcessStartInfo("dotnet", "bin/" +
                                                         "release" +
                                                         "/netcoreapp1.1/TestApp.dll " + clusterName)
                {
                    UseShellExecute = false
                };
                Process.Start(psi);
            }

            var tasks = new List<Task>();
            for (int i = 0; i < 2000; i++)
            {
                var client = Grains.HelloGrain("name" + i % 200);
                var task = client.SayHello(new HelloRequest()).ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        Console.Write(".");
                    }
                    else
                    {
                        Console.Write("#");
                    }
                });
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("Done!");
            Console.ReadLine();
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
}
