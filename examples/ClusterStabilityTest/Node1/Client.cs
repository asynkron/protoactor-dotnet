using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
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

            for (int i = 0; i < 50; i++)
            {
                var psi = new ProcessStartInfo("dotnet", "bin/debug/netcoreapp1.1/TestApp.dll " + clusterName)
                {
                    UseShellExecute = false
                };
                Process.Start(psi);
            }

            for (int i = 0; i < 10000; i++)
            {
                var client = Grains.HelloGrain("name" + i % 100);
                client.SayHello(new HelloRequest()).ContinueWith(t =>
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
            }
           
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
