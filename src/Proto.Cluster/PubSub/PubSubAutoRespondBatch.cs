// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Proto.Remote;

namespace Proto.Cluster.PubSub;

/// <summary>
///     Message posted to subscriber's mailbox, that is then unrolled to single messages, and has ability to auto respond
/// </summary>
/// <param name="Envelopes"></param>
public record PubSubAutoRespondBatch(IReadOnlyCollection<object> Envelopes) : IRootSerializable, IMessageBatch,
    IAutoRespond
{
    public object GetAutoResponse(IContext context) => new PublishResponse();

    public IReadOnlyCollection<object> GetMessages() => Envelopes;

    public IRootSerialized Serialize(ActorSystem system)
    {
        var batch = new PubSubAutoRespondBatchTransport();
        PubSubSerializationHelper.SerializeEnvelopes(system, Envelopes, batch.TypeNames, batch.Envelopes);
        return batch;
    }
}

public partial class PubSubAutoRespondBatchTransport : IRootSerialized
{
    public IRootSerializable Deserialize(ActorSystem system)
    {
        var messages = PubSubSerializationHelper.DeserializeEnvelopes(system, TypeNames, Envelopes);
        return new PubSubAutoRespondBatch(messages);
    }
}
