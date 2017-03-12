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
    }
}
