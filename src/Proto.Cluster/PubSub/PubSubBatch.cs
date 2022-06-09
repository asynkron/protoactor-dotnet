// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Cluster.PubSub;

public class PubSubBatch : IRootSerializable
{
    public List<object> Envelopes { get; } = new();

    internal List<TaskCompletionSource<bool>> DeliveryReports { get; } = new();

    internal List<CancellationToken> CancelTokens { get; } = new();

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

    public bool IsEmpty() => Envelopes.Count == 0;
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