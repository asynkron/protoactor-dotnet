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
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            remote = new Remote(system, serialization);
        }

        private static readonly Remote remote;
        private static readonly ActorSystem system;

        private static bool remoteStarted;

        public static (Remote, ActorSystem) EnsureRemote()
        {
            if (remoteStarted) return (remote, system);

            var config = new RemoteConfig
            {
                EndpointWriterOptions = new EndpointWriterOptions
                {
                    MaxRetries = 2,
                    RetryBackOff = TimeSpan.FromMilliseconds(10),
                    RetryTimeSpan = TimeSpan.FromSeconds(120)
                }
            };
            
            var service = new ProtoService(12000,"localhost");
            service.StartAsync().Wait();
            
            remote.Start(GetLocalIp(), 12001, config);
            
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