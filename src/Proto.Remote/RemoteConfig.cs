// -----------------------------------------------------------------------
//   <copyright file="RemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Google.Protobuf.Reflection;
using Grpc.Core;
using JetBrains.Annotations;

namespace Proto.Remote
{
    [PublicAPI]
    public class RemoteConfig
    {
        public const string AllInterfaces = "0.0.0.0";
        public const string Localhost = "127.0.0.1";
        public const int AnyFreePort = 0;
        
        public static RemoteConfig BinToAllInterfaces(string advertisedHost, int port = 0) =>
            new RemoteConfig(AllInterfaces, port).WithAdvertisedHost(advertisedHost);
        
        public static RemoteConfig BindToLocalhost(int port = 0) => new RemoteConfig(Localhost, port);
        
        public static RemoteConfig BindTo(string host, int port = 0) => new RemoteConfig(host, port);
        
        private RemoteConfig(string host, int port)
        {
            Host = host;
            Port = port;
        }

        /// <summary>
        ///     The host to listen to
        /// </summary>
        public string Host { get; }

        /// <summary>
        ///     The port to listen to, 0 means any free port
        /// </summary>
        public int Port { get; }

        /// <summary>
        ///     Known actor kinds that can be spawned remotely
        /// </summary>
        public Dictionary<string, Props> RemoteKinds { get; private set; } = new Dictionary<string, Props>();

        /// <summary>
        ///     Gets or sets the ChannelOptions for the gRPC channel.
        /// </summary>
        public IEnumerable<ChannelOption> ChannelOptions { get; set; } = null!;

        /// <summary>
        ///     Gets or sets the CallOptions for the gRPC channel.
        /// </summary>
        public CallOptions CallOptions { get; set; }

        /// <summary>
        ///     Gets or sets the ChannelCredentials for the gRPC channel. The default is Insecure.
        /// </summary>
        public ChannelCredentials ChannelCredentials { get; set; } = ChannelCredentials.Insecure;

        /// <summary>
        ///     Gets or sets the ServerCredentials for the gRPC server. The default is Insecure.
        /// </summary>
        public ServerCredentials ServerCredentials { get; set; } = ServerCredentials.Insecure;

        /// <summary>
        ///     Gets or sets the advertised hostname for the remote system.
        ///     If the remote system is behind e.g. a NAT or reverse proxy, this needs to be set to
        ///     the external hostname in order for other systems to be able to connect to it.
        /// </summary>
        public string? AdvertisedHostname { get; set; }

        /// <summary>
        ///     Gets or sets the advertised port for the remote system.
        ///     If the remote system is behind e.g. a NAT or reverse proxy, this needs to be set to
        ///     the external port in order for other systems to be able to connect to it.
        /// </summary>
        public int? AdvertisedPort { get; set; }

        public EndpointWriterOptions EndpointWriterOptions { get; set; } = new EndpointWriterOptions();

        public Serialization Serialization { get; set; } = new Serialization();


        public RemoteConfig WithChannelOptions(IEnumerable<ChannelOption> options)
        {
            ChannelOptions = options;
            return this;
        }

        public RemoteConfig WithCallOptions(CallOptions options)
        {
            CallOptions = options;
            return this;
        }

        public RemoteConfig WithChannelCredentials(ChannelCredentials channelCredentials)
        {
            ChannelCredentials = channelCredentials;
            return this;
        }

        public RemoteConfig WithServerCredentials(ServerCredentials serverCredentials)
        {
            ServerCredentials = serverCredentials;
            return this;
        }

        public RemoteConfig WithAdvertisedHost(string? advertisedHostname)
        {
            AdvertisedHostname = advertisedHostname;
            return this;
        }

        /// <summary>
        /// Advertised port can be different from the bound port, e.g. in container scenarios
        /// </summary>
        /// <param name="advertisedPort"></param>
        /// <returns></returns>
        public RemoteConfig WithAdvertisedPort(int? advertisedPort)
        {
            AdvertisedPort = advertisedPort;
            return this;
        }

        public RemoteConfig WithEndpointWriterBatchSize(int endpointWriterBatchSize)
        {
            EndpointWriterOptions.EndpointWriterBatchSize = endpointWriterBatchSize;
            return this;
        }

        public RemoteConfig WithEndpointWriterMaxRetries(int endpointWriterMaxRetries)
        {
            EndpointWriterOptions.MaxRetries = endpointWriterMaxRetries;
            return this;
        }

        public RemoteConfig WithEndpointWriterRetryTimeSpan(TimeSpan endpointWriterRetryTimeSpan)
        {
            EndpointWriterOptions.RetryTimeSpan = endpointWriterRetryTimeSpan;
            return this;
        }

        public RemoteConfig WithEndpointWriterRetryBackOff(TimeSpan endpointWriterRetryBackoff)
        {
            EndpointWriterOptions.RetryBackOff = endpointWriterRetryBackoff;
            return this;
        }

        public RemoteConfig WithProtoMessages(params FileDescriptor[] fileDescriptors)
        {
            foreach (var fd in fileDescriptors) Serialization.RegisterFileDescriptor(fd);
            return this;
        }
        
        public RemoteConfig WithRemoteKind(string kind, Props prop)
        {
            RemoteKinds.Add(kind, prop);
            return this;
        }

        public RemoteConfig WithRemoteKinds(params (string kind, Props prop)[] knownKinds)
        {
            foreach (var (kind, prop) in knownKinds) RemoteKinds.Add(kind, prop);
            return this;
        }
        
        public RemoteConfig WithSerializer(ISerializer serializer, bool makeDefault = false)
        {
            Serialization.RegisterSerializer(serializer,makeDefault);
            return this;
        }
    }
}