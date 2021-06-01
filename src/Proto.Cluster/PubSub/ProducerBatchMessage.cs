// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Cluster
{
    public class ProducerBatchMessage :  IRootSerializable
    {
        public List<object> Envelopes { get; } = new ();

        public List<TaskCompletionSource<bool>> DeliveryReports { get; } = new();

        public IRootSerialized Serialize(ActorSystem system)
        {
            var s = system.Serialization();

            var batch = new ProducerBatch();
            foreach (var message in Envelopes)
            {
                var (messageData, typeName, serializerId) = s.Serialize(message);
                var typeIndex = batch.TypeNames.IndexOf(typeName);

                if (typeIndex == -1)
                {
                    batch.TypeNames.Add(typeName);
                    typeIndex = batch.TypeNames.Count - 1;
                }

                var producerMessage = new ProducerEnvelope
                {
                    MessageData = messageData,
                    TypeId = typeIndex,
                };
                
                batch.Envelopes.Add(producerMessage);
            }

            return batch;
        }
    }

    public partial class ProducerBatch : IRootSerialized
    {
        public IRootSerializable Deserialize(ActorSystem system)
        {
            var ser = system.Serialization();
            //deserialize messages in the envelope
            var messages = Envelopes
                .Select(e => ser
                    .Deserialize(TypeNames[e.TypeId], e.MessageData, e.SerializerId))
                .ToList();

            var res = new ProducerBatchMessage();
            res.Envelopes.AddRange(messages);
            return res;
        }
    }
}