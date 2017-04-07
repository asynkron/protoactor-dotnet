using System;
using System.Threading;
using Xunit;

namespace Proto.Remote.Tests
{
    public class RemoteActorSystem : IDisposable
    {
        private static string NodeAppPath => @"Proto.Remote.Tests.Node/bin/Debug/netcoreapp1.1/Proto.Remote.Tests.Node.dll";

        private readonly System.Diagnostics.Process _process;

        public RemoteActorSystem()
        {
            var testsDirectory = System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent;
           
            var nodeDllPath = $@"{testsDirectory.FullName}/{NodeAppPath}";
            Console.WriteLine($"NodeDLL path: {nodeDllPath}");
            _process = new System.Diagnostics.Process
            {
                StartInfo =
                {
                    Arguments = nodeDllPath,
                    CreateNoWindow = false,
                    UseShellExecute = false,
                    FileName = "dotnet"
                }
            };
            _process.Start();
            Remote.Start("127.0.0.1", 12001);

            Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);

            Thread.Sleep(2000);
        }

        public void Dispose()
        {
            if (_process == null || _process.HasExited)
                return;

            _process.Kill();
        }
    }

    [CollectionDefinition("RemoteTests")]
    public class RemoteCollection : ICollectionFixture<RemoteActorSystem>
    {
    }
}
