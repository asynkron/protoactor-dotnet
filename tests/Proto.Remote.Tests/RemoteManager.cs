using System;
using System.Net;
using System.Net.Sockets;

namespace Proto.Remote.Tests
{
    public static class RemoteManager
    {
        public const string RemoteAddress = "localhost:12000";
        static RemoteManager()
        {
            system = new ActorSystem();
            remote = new Remote(system);
        }

        private static readonly Remote remote;
        private static readonly ActorSystem system;

        private static bool remoteStarted;

        public static (Remote, ActorSystem) EnsureRemote()
        {
            if (remoteStarted) return (remote, system);

            var config = 
                new RemoteConfig(GetLocalIp(), 12001)
                .WithEndpointWriterMaxRetries(2)
                .WithEndpointWriterRetryBackOff( TimeSpan.FromMilliseconds(10))
                .WithEndpointWriterRetryTimeSpan(TimeSpan.FromSeconds(120))
                .WithProtoMessages(Messages.ProtosReflection.Descriptor);
            
            var service = new ProtoService(12000,"localhost");
            service.StartAsync().Wait();
            
            remote.StartAsync(config);
            
            remoteStarted = true;

            return (remote, system);

            static string GetLocalIp()
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);

                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString();
            }
        }
    }
}