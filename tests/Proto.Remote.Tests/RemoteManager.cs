using System;
using System.Net;
using System.Net.Sockets;

namespace Proto.Remote.Tests
{
    public class RemoteManager
    {
        public const string RemoteAddress = "0.0.0.0:12000";
        static RemoteManager()
        {
            system = new ActorSystem();
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            remote = new Remote(system, serialization);
        }

        private static Remote remote;
        private static ActorSystem system;

        private static bool remoteStarted;

        public static (Remote, ActorSystem) EnsureRemote()
        {
            if (remoteStarted) return (remote, system);

            var config = new RemoteConfig
            {
                EndpointWriterOptions = new EndpointWriterOptions
                {
                    MaxRetries = 2,
                    RetryBackOffms = 10,
                    RetryTimeSpan = TimeSpan.FromSeconds(120)
                }
            };
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