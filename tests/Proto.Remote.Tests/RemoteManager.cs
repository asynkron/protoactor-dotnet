using System;
using System.Net;
using System.Net.Sockets;

namespace Proto.Remote.Tests
{
    public class RemoteManager
    {
        public const string RemoteAddress = "localhost:12000";
        static RemoteManager()
        {
            system = new ActorSystem();
            remote = new SelfHostedRemote(system, 12001, remote =>
            {
                remote.Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
                remote.RemoteConfig.EndpointWriterOptions = new EndpointWriterOptions
                {
                    MaxRetries = 2,
                    RetryBackOffms = 10,
                    RetryTimeSpan = TimeSpan.FromSeconds(120)
                };
            });
        }

        private static readonly IRemote remote;
        private static readonly ActorSystem system;

        private static bool remoteStarted;

        public static (IRemote, ActorSystem) EnsureRemote()
        {
            if (remoteStarted) return (remote, system);

            var service = new ProtoService(12000, "localhost");
            service.StartAsync().Wait();

            remote.Start();

            remoteStarted = true;

            return (remote, system);
        }
    }
}