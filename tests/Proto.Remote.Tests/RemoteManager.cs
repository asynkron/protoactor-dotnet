using System;
using System.Net;
using System.Net.Sockets;

namespace Proto.Remote.Tests
{
    public static class RemoteManager
    {
        public const string RemoteAddress = "0.0.0.0:12000";
        
        static RemoteManager() => Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);

        private static bool remoteStarted;

        public static void EnsureRemote()
        {
            if (remoteStarted) return;
            
            var config = new RemoteConfig
            {
                EndpointWriterOptions = new EndpointWriterOptions
                {
                    MaxRetries = 2,
                    RetryBackOffms = 10,
                    RetryTimeSpan = TimeSpan.FromSeconds(120)
                }
            };

            Remote.Start(GetLocalIp(), 12001, config);
            remoteStarted = true;

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