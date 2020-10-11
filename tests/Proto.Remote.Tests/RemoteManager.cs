using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Proto.Remote.Tests
{
    public static class RemoteManager
    {
        public const string RemoteAddress = "localhost:12000";

        public static async Task<(Remote, ActorSystem)> EnsureRemote()
        {
            var system = new ActorSystem();

            var config =
                RemoteConfig.BindToLocalhost()
                    .WithEndpointWriterMaxRetries(2)
                    .WithEndpointWriterRetryBackOff(TimeSpan.FromMilliseconds(10))
                    .WithEndpointWriterRetryTimeSpan(TimeSpan.FromSeconds(120))
                    .WithProtoMessages(Messages.ProtosReflection.Descriptor);

            var service = new ProtoService(12000, "localhost");
            service.StartAsync().Wait();

            var remote = new Remote(system, config);
            await remote.StartAsync();

            return (remote, system);
        }
    }
}