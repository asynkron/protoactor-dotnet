// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using Proto.Remote;

namespace Proto.Cluster
{
    public record TopicBatchMessage(IReadOnlyCollection<object> Envelopes) :  IRootSerializable , IMessageBatch, IAutoRespond
    {
        public object GetAutoResponse() => new PublishResponse();
        
        public IReadOnlyCollection<object> GetMessages() => Envelopes;
        
        public IRootSerialized Serialize(ActorSystem system)
        {
            var s = system.Serialization();

            var batch = new TopicBatchRequest();
            foreach (var message in Envelopes)
            {
                
                var (messageData, typeName, serializerId) = s.Serialize(message);
                var typeIndex = batch.TypeNames.IndexOf(typeName);

                if (typeIndex == -1)
                {
                    batch.TypeNames.Add(typeName);
                    typeIndex = batch.TypeNames.Count - 1;
                }

                var topicEnvelope = new TopicEnvelope
                {
                    MessageData = messageData,
                    TypeId = typeIndex,
                    SerializerId = serializerId,
                };
                
                batch.Envelopes.Add(topicEnvelope);
            }

            return batch;
        }
    }

    public partial class TopicBatchRequest : IRootSerialized
    {
        public IRootSerializable Deserialize(ActorSystem system)
        {
            var ser = system.Serialization();
            //deserialize messages in the envelope
            var messages = Envelopes
                .Select(e => ser
                    .Deserialize(TypeNames[e.TypeId], e.MessageData, e.SerializerId))
                .ToList();

            return new TopicBatchMessage(messages);
        }
    }
}