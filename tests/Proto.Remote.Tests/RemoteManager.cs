using System;
using System.Net;
using System.Net.Sockets;

namespace Proto.Remote.Tests
{
    public class RemoteManager
    {
        public static ActorSystem GetLocalSystem()
        {
            var system = new ActorSystem();
            var remote = new SelfHostedRemote(system, "127.0.0.1", 0, remote =>
            {
                remote.Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
                remote.RemoteConfig.EndpointWriterOptions = new EndpointWriterOptions
                {
                    MaxRetries = 2,
                    RetryBackOffms = 10,
                    RetryTimeSpan = TimeSpan.FromSeconds(120)
                };
            });
            remote.Start();
            return system;
        }
        public static ActorSystem GetDistantSystem()
        {
            var props = Props.FromProducer(() => new EchoActor());
            var actorSystem = new ActorSystem();
            var serialization = new Serialization();
            var remote = new SelfHostedRemote(actorSystem, "127.0.0.1", 0, remote =>
            {
                remote.Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
                remote.RemoteConfig.EndpointWriterOptions = new EndpointWriterOptions
                {
                    MaxRetries = 2,
                    RetryBackOffms = 10,
                    RetryTimeSpan = TimeSpan.FromSeconds(120)
                };
                remote.RemoteKindRegistry.RegisterKnownKind("EchoActor", props);
            });
            remote.Start();
            actorSystem.Root.SpawnNamed(props, "EchoActorInstance");
            return actorSystem;
        }
    }
}