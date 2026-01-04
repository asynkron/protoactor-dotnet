// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
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
        var batch = new PubSubBatchTransport();
        PubSubSerializationHelper.SerializeEnvelopes(system, Envelopes, batch.TypeNames, batch.Envelopes);
        return batch;
    }
}

public partial class PubSubBatchTransport : IRootSerialized
{
    public IRootSerializable Deserialize(ActorSystem system)
    {
        var messages = PubSubSerializationHelper.DeserializeEnvelopes(system, TypeNames, Envelopes);
        var res = new PubSubBatch();
        res.Envelopes.AddRange(messages);
        return res;
    }
}
