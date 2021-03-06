// -----------------------------------------------------------------------
// <copyright file="Publisher.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Cluster.PubSub
{
    public class Producer
    {
        private readonly Cluster _cluster;
        private ProducerBatch _batch;
        private string _topic;

        public Producer(Cluster cluster, string topic)
        {
            _cluster = cluster;
            _topic = topic;
            _batch = new ProducerBatch();
        }

        public void Produce(object message)
        {
            var s = _cluster.System.Serialization();
            
            var typeName = s.GetTypeName(message,s.DefaultSerializerId);
            var messageData = s.Serialize(message,s.DefaultSerializerId);
            var typeIndex = _batch.TypeNames.IndexOf(typeName);

            if (typeIndex == -1)
            {
                _batch.TypeNames.Add(typeName);
                typeIndex = _batch.TypeNames.Count - 1;
            }

            var producerMessage = new ProducerEnvelope
            {
                MessageData = messageData,
                TypeId = typeIndex,
            };
            
            _batch.Envelopes.Add(producerMessage);
        }

        public async Task WhenAllPublished()
        {
            await _cluster.RequestAsync<PublishResponse>(_topic, "topic", _batch, CancellationToken.None);
            _batch = new ProducerBatch();
        }
    }
}