// -----------------------------------------------------------------------
//   <copyright file="RemoteConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Grpc.Core;

namespace Proto.Remote
{
    public class RemoteConfig
    {
        /// <summary>
        /// Gets or sets the batch size for the endpoint writer. The default value is 1000.
        /// The endpoint writer will send up to this number of messages in a batch.
        /// </summary>
        public int EndpointWriterBatchSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the ChannelOptions for the gRPC channel.
        /// </summary>
        public IEnumerable<ChannelOption> ChannelOptions { get; set; }

        /// <summary>
        /// Gets or sets the CallOptions for the gRPC channel.
        /// </summary>
        public CallOptions CallOptions { get; set; }

        /// <summary>
        /// Gets or sets the ChannelCredentials for the gRPC channel. The default is Insecure.
        /// </summary>
        public ChannelCredentials ChannelCredentials { get; set; } = ChannelCredentials.Insecure;

        /// <summary>
        /// Gets or sets the ServerCredentials for the gRPC server. The default is Insecure.
        /// </summary>
        public ServerCredentials ServerCredentials { get; set; } = ServerCredentials.Insecure;

        /// <summary>
        /// Gets or sets the advertised hostname for the remote system.
        /// If the remote system is behind e.g. a NAT or reverse proxy, this needs to be set to
        /// the external hostname in order for other systems to be able to connect to it.
        /// </summary>
        public string AdvertisedHostname { get; set; }

        /// <summary>
        /// Gets or sets the advertised port for the remote system.
        /// If the remote system is behind e.g. a NAT or reverse proxy, this needs to be set to
        /// the external port in order for other systems to be able to connect to it.
        /// </summary>
        public int? AdvertisedPort { get; set; }
    }
}
