using System.Collections.Generic;
using Confluent.Kafka;

namespace Proto.Cluster.PubSub.Kafka
{
    public class Payload<TMessage>
    {
        public IReadOnlyCollection<TMessage> Messages { get; init; }
        public IReadOnlyCollection<ConsumeResult<Ignore, byte[]>> Results { get; init; }
    }
}