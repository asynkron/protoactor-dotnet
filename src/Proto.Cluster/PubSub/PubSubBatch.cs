// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Proto.Remote;

namespace Proto.Cluster.PubSub;

/// <summary>
///     Represents a batch of messages that are published to a topic.
/// </summary>
/// <remarks>
///     Due to how publishing works, do not attempt modifying contents of the batch after it has been published. The batch
///     may still be
///     in the send pipeline, waiting to be serialized (or delivered to local subscribers). The batch is not immutable to
///     avoid the overhead.
/// </remarks>
public class PubSubBatch : IRootSerializable
{
    public List<object> Envelopes { get; } = new();

    public IRootSerialized Serialize(ActorSystem system)
    {
        var s = system.Serialization();

        var batch = new PubSubBatchTransport();

        foreach (var message in Envelopes)
        {
            var (messageData, typeName, serializerId) = s.Serialize(message);
            var typeIndex = batch.TypeNames.IndexOf(typeName);

            if (typeIndex == -1)
            {
                batch.TypeNames.Add(typeName);
                typeIndex = batch.TypeNames.Count - 1;
            }

            var envelope = new PubSubEnvelope
            {
                MessageData = messageData,
                TypeId = typeIndex,
                SerializerId = serializerId
            };

            batch.Envelopes.Add(envelope);
        }

        return batch;
    }
}

public partial class PubSubBatchTransport : IRootSerialized
{
    public IRootSerializable Deserialize(ActorSystem system)
    {
        var ser = system.Serialization();

        //deserialize messages in the envelope
        var messages = Envelopes
            .Select(e => ser
                .Deserialize(TypeNames[e.TypeId], e.MessageData, e.SerializerId)
            )
            .ToList();

        var res = new PubSubBatch();
        res.Envelopes.AddRange(messages);

        return res;
    }
}