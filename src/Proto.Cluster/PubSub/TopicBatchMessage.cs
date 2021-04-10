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
    public class TopicBatchMessage :  IRootSerializable , IMessageBatch, IAutoRespond
    {
        public object GetAutoResponse() => new PublishResponse();
        public TopicBatchMessage(IReadOnlyCollection<object> envelopes)
        {
            Envelopes = envelopes;
        }
        public IReadOnlyCollection<object> Envelopes { get; }

        public IReadOnlyCollection<object> GetMessages() => Envelopes;
        

        public IRootSerialized Serialize(ActorSystem system)
        {
            var s = system.Serialization();

            var batch = new TopicBatch();
            foreach (var message in Envelopes)
            {
                
                var typeName = s.GetTypeName(message, s.DefaultSerializerId);
                var messageData = s.Serialize(message, s.DefaultSerializerId);
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
                };
                
                batch.Envelopes.Add(topicEnvelope);
            }

            return batch;
        }
    }

    public partial class TopicBatch : IRootSerialized
    {
        public IRootSerializable Deserialize(ActorSystem system)
        {
            var ser = system.Serialization();
            //deserialize messages in the envelope
            var messages = Envelopes
                .Select(e => ser
                    .Deserialize(TypeNames[e.TypeId], e.MessageData, ser.DefaultSerializerId))
                .ToList();

            return new TopicBatchMessage(messages);
        }
    }
}