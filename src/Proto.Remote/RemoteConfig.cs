// -----------------------------------------------------------------------
//   <copyright file="RemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Google.Protobuf.Reflection;
using Grpc.Core;

namespace Proto.Remote
{
    public class RemoteConfig
    {
        public RemoteConfig(string hostname, int port)
        {
            Hostname = hostname;
            Port = port;
        }

        public string Hostname { get; set; }
        public int Port { get; set; }
        
        /// <summary>
        ///     Known actor kinds that can be spawned remotely
        /// </summary>
        public Dictionary<string,Props> KnownKinds { get; set; } = new Dictionary<string, Props>();
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
    }

    public class EndpointWriterOptions
    {
        /// <summary>
        ///     Gets or sets the batch size for the endpoint writer. The default value is 1000.
        ///     The endpoint writer will send up to this number of messages in a batch.
        /// </summary>
        public int EndpointWriterBatchSize { get; set; } = 1000;

        /// <summary>
        ///     the number of times to retry the connection within the RetryTimeSpan
        /// </summary>
        public int MaxRetries { get; set; } = 8;

        /// <summary>
        ///     the timespan that restarts are counted withing.
        ///     meaning that the retry counter resets after this timespan if no errors.
        /// </summary>
        public TimeSpan RetryTimeSpan { get; set; } = TimeSpan.FromMinutes(3);

        /// <summary>
        ///     each retry backs off by an exponential ratio of this timespan
        /// </summary>
        public TimeSpan RetryBackOff { get; set; } = TimeSpan.FromMilliseconds(100);
    }

    public static class FluentRemoteConfig
    {
        public static RemoteConfig WithChannelOptions(this RemoteConfig self,IEnumerable<ChannelOption> options)
        {
            self.ChannelOptions = options;
            return self;
        }
        
        public static RemoteConfig WithCallOptions(this RemoteConfig self,CallOptions options)
        {
            self.CallOptions = options;
            return self;
        }
        
        public static RemoteConfig WithChannelCredentials(this RemoteConfig self,ChannelCredentials channelCredentials)
        {
            self.ChannelCredentials = channelCredentials;
            return self;
        }
        
        public static RemoteConfig WithServerCredentials(this RemoteConfig self,ServerCredentials serverCredentials)
        {
            self.ServerCredentials = serverCredentials;
            return self;
        }
        
        public static RemoteConfig WithAdvertisedHostname(this RemoteConfig self,string? advertisedHostname)
        {
            self.AdvertisedHostname = advertisedHostname;
            return self;
        }
        
        public static RemoteConfig WithAdvertisedPort(this RemoteConfig self,int? advertisedPort)
        {
            self.AdvertisedPort = advertisedPort;
            return self;
        }
        
        public static RemoteConfig WithEndpointWriterBatchSize(this RemoteConfig self,int endpointWriterBatchSize)
        {
            self.EndpointWriterOptions.EndpointWriterBatchSize = endpointWriterBatchSize;
            return self;
        }
        
        public static RemoteConfig WithEndpointWriterMaxRetries(this RemoteConfig self,int endpointWriterMaxRetries)
        {
            self.EndpointWriterOptions.MaxRetries = endpointWriterMaxRetries;
            return self;
        }
        
        public static RemoteConfig WithEndpointWriterRetryTimeSpan(this RemoteConfig self,TimeSpan endpointWriterRetryTimeSpan)
        {
            self.EndpointWriterOptions.RetryTimeSpan = endpointWriterRetryTimeSpan;
            return self;
        }
        
        public static RemoteConfig WithEndpointWriterRetryBackOff(this RemoteConfig self,TimeSpan endpointWriterRetryBackoff)
        {
            self.EndpointWriterOptions.RetryBackOff = endpointWriterRetryBackoff;
            return self;
        }

        public static RemoteConfig WithProtoMessages(this RemoteConfig self,params FileDescriptor[] fileDescriptors)
        {
            foreach (var fd in fileDescriptors)
            {
                self.Serialization.RegisterFileDescriptor(fd);
            }

            return self;
        }
        
        public static RemoteConfig WithKnownKinds(this RemoteConfig self,params (string kind,Props prop)[] knownKinds)
        {
            foreach (var kind in knownKinds)
            {
                self.KnownKinds.Add(kind.kind,kind.prop);
            }

            return self;
        }
        
    }
}