// -----------------------------------------------------------------------
//   <copyright file="MessageBatchFactory.cs" company="Asynkron AB">
//       Copyright (C) 2015-2025 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Diagnostics;
using Proto.Extensions;
using Proto.Remote.Metrics;

namespace Proto.Remote;

internal static class MessageBatchFactory
{
    private static readonly ILogger Logger = Log.CreateLogger("MessageBatchFactory");

    internal static MessageBatch CreateBatch(ActorSystem system, RemoteConfig remoteConfig,
        IReadOnlyCollection<RemoteDeliver> messages)
    {
        var envelopes = new List<MessageEnvelope>(messages.Count);
        var typeNames = new Dictionary<string, int>();
        var targets = new Dictionary<(string address, string id), int>();
        var targetList = new List<string>();
        var typeNameList = new List<string>();
        var senders = new Dictionary<(string address, string id), int>();
        var senderList = new List<PID>();

        foreach (var rd in messages)
        {
            var target = rd.Target;
            var targetKey = (target.Address, target.Id);

            if (!targets.TryGetValue(targetKey, out var targetId))
            {
                targetId = targets[targetKey] = targets.Count;
                targetList.Add(target.Id);
            }

            var senderId = 0;
            var sender = rd.Sender;

            if (sender != null)
            {
                var senderKey = (sender.Address, sender.Id);

                if (!senders.TryGetValue(senderKey, out senderId))
                {
                    senderId = senders[senderKey] = senders.Count + 1;

                    senderList.Add(sender.Address == system.Address
                        ? PID.FromAddress("", sender.Id)
                        : PID.FromAddress(sender.Address, sender.Id));
                }
            }

            var message = rd.Message;

            //if the message can be translated to a serialization representation, we do this here
            //this only apply to root level messages and never to nested child objects inside the message
            if (message is IRootSerializable deserialized)
            {
                message = deserialized.Serialize(system);
            }

            if (message is null)
            {
                Logger.LogError("Null message passed to EndpointActor, ignoring message");
                continue;
            }

            ByteString bytes;
            string typeName;
            int serializerId;

            try
            {
                (bytes, typeName, serializerId) = remoteConfig.Serialization.Serialize(message);
            }
            catch (CodedOutputStream.OutOfSpaceException oom)
            {
                system.Diagnostics.RegisterEvent("Remote",
                    $"Message is too large {message.GetMessageTypeName()}");
                Logger.LogError(oom, "Message is too large {MessagePayload}", message.GetMessageTypeName());
                throw;
            }
            catch (Exception x)
            {
                system.Diagnostics.RegisterEvent("Remote",
                    $"Missing serializer for {message.GetMessageTypeName()}");
                Logger.LogError(x, "Serialization failed for message {MessagePayload}",
                    message.GetMessageTypeName());
                throw;
            }

            if (system.Metrics.Enabled)
            {
                RemoteMetrics.RemoteSerializedMessageCount.Add(1,
                    new KeyValuePair<string, object?>("id", system.Id),
                    new KeyValuePair<string, object?>("address", system.Address),
                    new KeyValuePair<string, object?>("messagetype", typeName)
                );
            }

            if (!typeNames.TryGetValue(typeName, out var typeId))
            {
                typeId = typeNames[typeName] = typeNames.Count;
                typeNameList.Add(typeName);
            }

            MessageHeader? header = null;

            if (rd.Header?.Count > 0)
            {
                header = new MessageHeader();
                header.HeaderData.Add(rd.Header.ToDictionary());
            }

            var envelope = new MessageEnvelope
            {
                MessageData = bytes,
                Sender = senderId,
                Target = targetId,
                TypeId = typeId,
                SerializerId = serializerId,
                MessageHeader = header,
                TargetRequestId = rd.Target.RequestId,
                SenderRequestId = sender?.RequestId ?? default
            };

            // if (Logger.IsEnabled(LogLevel.Trace))
            //     Logger.LogTrace("[{SystemAddress}] Endpoint adding Envelope {Envelope}", system.Address, envelope);
            envelopes.Add(envelope);
        }

        var batch = new MessageBatch
        {
            Targets = { targetList },
            TypeNames = { typeNameList },
            Envelopes = { envelopes },
            Senders = { senderList }
        };

        // if (Logger.IsEnabled(LogLevel.Trace))
        //     Logger.LogTrace("[{SystemAddress}] Sending {Count} envelopes for {Address}", system.Address, envelopes.Count, address);
        return batch;
    }
}
