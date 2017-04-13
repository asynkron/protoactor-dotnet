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
            StartRemote("127.0.0.1", 12000);
            
            Remote.Start("127.0.0.1", 12001);
            Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
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

        public (string Address, System.Diagnostics.Process Process) StartRemote(string host = "127.0.0.1", int port = 12000)
        {
            string buildConfig = "Debug";
#if RELEASE
            buildConfig = "Release";
#endif
            var nodeAppPath = $@"Proto.Remote.Tests.Node/bin/{buildConfig}/netcoreapp1.1/Proto.Remote.Tests.Node.dll";
            var testsDirectory = System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent;
            var nodeDllPath = $@"{testsDirectory.FullName}/{nodeAppPath}";
            Console.WriteLine($"NodeDLL path: {nodeDllPath}");
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
            Nodes.Add($"{host}:{port}", process);
            return ($"{host}:{port}", process);
        }
    }

    [CollectionDefinition("RemoteTests")]
    public class RemoteCollection : ICollectionFixture<RemoteManager>
    {
    }
}
