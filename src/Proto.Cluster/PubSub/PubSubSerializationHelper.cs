// -----------------------------------------------------------------------
// <copyright file="PubSubSerializationHelper.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Google.Protobuf.Collections;
using Proto.Remote;

namespace Proto.Cluster.PubSub;

/// <summary>
///     Helper class for shared PubSub batch serialization logic.
/// </summary>
internal static class PubSubSerializationHelper
{
    /// <summary>
    ///     Serializes a collection of messages into PubSub envelopes.
    /// </summary>
    /// <param name="system">The actor system for serialization</param>
    /// <param name="messages">The messages to serialize</param>
    /// <param name="typeNames">The type names collection to populate</param>
    /// <param name="envelopes">The envelopes collection to populate</param>
    public static void SerializeEnvelopes(
        ActorSystem system,
        IEnumerable<object> messages,
        RepeatedField<string> typeNames,
        RepeatedField<PubSubEnvelope> envelopes)
    {
        var serialization = system.Serialization();

        foreach (var message in messages)
        {
            var (messageData, typeName, serializerId) = serialization.Serialize(message);
            var typeIndex = typeNames.IndexOf(typeName);

            if (typeIndex == -1)
            {
                typeNames.Add(typeName);
                typeIndex = typeNames.Count - 1;
            }

            var envelope = new PubSubEnvelope
            {
                MessageData = messageData,
                TypeId = typeIndex,
                SerializerId = serializerId
            };

            envelopes.Add(envelope);
        }
    }

    /// <summary>
    ///     Deserializes PubSub envelopes back into messages.
    /// </summary>
    /// <param name="system">The actor system for deserialization</param>
    /// <param name="typeNames">The type names from the transport</param>
    /// <param name="envelopes">The envelopes to deserialize</param>
    /// <returns>The deserialized messages</returns>
    public static List<object> DeserializeEnvelopes(
        ActorSystem system,
        RepeatedField<string> typeNames,
        RepeatedField<PubSubEnvelope> envelopes)
    {
        var serialization = system.Serialization();
        var messages = new List<object>(envelopes.Count);

        foreach (var envelope in envelopes)
        {
            var message = serialization.Deserialize(
                typeNames[envelope.TypeId],
                envelope.MessageData,
                envelope.SerializerId);
            messages.Add(message);
        }

        return messages;
    }
}
