using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;

namespace Proto.Remote.Tests
{
    public class RemoteManager : IDisposable
    {
        private static string DefaultNodeAddress = "127.0.0.1:12000";
        public Dictionary<string, System.Diagnostics.Process> Nodes = new Dictionary<string, System.Diagnostics.Process>();

        public (string Address, System.Diagnostics.Process Process) DefaultNode => (DefaultNodeAddress, Nodes[DefaultNodeAddress]);

        public RemoteManager()
        {
            Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            ProvisionNode("127.0.0.1", 12000);
            Remote.Start("127.0.0.1", 12001);
            
            Thread.Sleep(3000);
        }

        public void Dispose()
        {
            foreach (var (_, process) in Nodes)
            {
                if (process != null && !process.HasExited)
                    process.Kill();
            }
        }

        public (string Address, System.Diagnostics.Process Process) ProvisionNode(string host = "127.0.0.1", int port = 12000)
        {
            var address = $"{host}:{port}";
            var buildConfig = "Debug";
#if RELEASE
            buildConfig = "Release";
#endif
            var nodeAppPath = $@"Proto.Remote.Tests.Node/bin/{buildConfig}/netcoreapp2.0/Proto.Remote.Tests.Node.dll";
            var testsDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent;
            var nodeDllPath = $@"{testsDirectory.FullName}/{nodeAppPath}";
            
            if (!File.Exists(nodeDllPath))
            {
                throw new FileNotFoundException(nodeDllPath);
            }

            var process = new System.Diagnostics.Process
            {
                StartInfo =
                {
                    Arguments = $"{nodeDllPath} --host {host} --port {port}",
                    CreateNoWindow = false,
                    UseShellExecute = false,
                    FileName = "dotnet"
                }
            };
            
            process.Start();
            Nodes.Add(address, process);
            
            Console.WriteLine($"Waiting for remote node {address} to initialise...");
            Thread.Sleep(TimeSpan.FromSeconds(3));

            return (address, process);
        }
    }

    [CollectionDefinition("RemoteTests")]
    public class RemoteCollection : ICollectionFixture<RemoteManager>
    {
    }
}
