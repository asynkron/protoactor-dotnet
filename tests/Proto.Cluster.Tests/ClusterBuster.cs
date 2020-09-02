using System;
using System.Net;
using System.Net.Sockets;
using Proto.Remote;

namespace Proto.Cluster.Tests
{
    public static class ClusterBuster
    {
        public static IRemote SpawnNode(int port)
        {
            var system = new ActorSystem();
            var serialization = new Serialization();
            var remote = new SelfHostedRemote(system, port, remote =>
            {
                remote.RemoteConfig.EndpointWriterOptions = new EndpointWriterOptions
                {
                    MaxRetries = 2,
                    RetryBackOffms = 10,
                    RetryTimeSpan = TimeSpan.FromSeconds(120)
                };
            });

            remote.Start();
            return remote;
        }

        private static string GetLocalIp()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);

            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString();
        }
    }
}