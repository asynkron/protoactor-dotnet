// -----------------------------------------------------------------------
//   <copyright file="RemoteMessageHandler.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Proto.Mailbox;
using Proto.Remote.Metrics;

namespace Proto.Remote
{
    public class RemoteMessageHandler
    {
        private readonly ILogger _logger = Log.CreateLogger<RemoteMessageHandler>();
        private readonly EndpointManager _endpointManager;
        private readonly ActorSystem _system;
        private readonly Serialization _serialization;
        private readonly LogLevel _deserializationErrorLogLevel;

        public RemoteMessageHandler(EndpointManager endpointManager, ActorSystem system, Serialization serialization, RemoteConfigBase remoteConfig)
        {
            _endpointManager = endpointManager;
            _system = system;
            _serialization = serialization;
            _deserializationErrorLogLevel = remoteConfig.DeserializationErrorLogLevel;
        }

        public void HandleRemoteMessage(RemoteMessage currentMessage)
        {
            switch (currentMessage.MessageTypeCase)
            {
                case RemoteMessage.MessageTypeOneofCase.MessageBatch: {
                    var batch = currentMessage.MessageBatch;

                    for (var i = 0; i < batch.Targets.Count; i++)
                    {
                        if (batch.Targets[i].TryTranslateToLocalClientPID(out var pid))
                        {
                            batch.Targets[i] = pid;
                        }
                        else
                        {
                            batch.Targets[i].Ref(_system);
                        }
                    }

                    var typeNames = batch.TypeNames.ToArray();

                    var m = RemoteMetrics.RemoteDeserializedMessageCount;

                    foreach (var envelope in batch.Envelopes)
                    {
                        var target = batch.Targets[envelope.Target];

                        if (envelope.TargetRequestId != default)
                        {
                            target = target.WithRequestId(envelope.TargetRequestId);
                        }

                        var sender = envelope.Sender == 0 ? null : batch.Senders[envelope.Sender - 1];

                        if (envelope.SenderRequestId != default)
                        {
                            sender = sender?.WithRequestId(envelope.SenderRequestId);
                        }

                        var typeName = typeNames[envelope.TypeId];

                        if (_system.Metrics.Enabled)
                            m.Add(1, new("id", _system.Id), new("address", _system.Address), new("messagetype", typeName));

                        object message;

                        try
                        {
                            message = _serialization.Deserialize(typeName, envelope.MessageData, envelope.SerializerId);

                            //translate from on-the-wire representation to in-process representation
                            //this only applies to root level messages, and never on nested child messages
                            if (message is IRootSerialized serialized) message = serialized.Deserialize(_system);
                        }
                        catch (Exception)
                        {
                            _logger.Log(_deserializationErrorLogLevel, "[{SystemAddress}] Unable to deserialize message with {Type}", _system.Address,
                                typeName
                            );
                            continue;
                        }

                        switch (message)
                        {
                            case Terminated msg:
                                if (_logger.IsEnabled(LogLevel.Trace))
                                    _logger.LogTrace("[{SystemAddress}] Received message {MessageType} {Message} for {Target}", _system.Address,
                                        msg.GetType().Name, msg, target
                                    );
                                var endpoint = msg.Who.TryGetSystemId(_system, out var systemId)
                                    ? _endpointManager.GetClientEndpoint(systemId)
                                    : _endpointManager.GetServerEndpoint(msg.Who.Address);
                                endpoint.RemoteTerminate(target, msg);
                                break;
                            case SystemMessage sys:
                                if (_logger.IsEnabled(LogLevel.Trace))
                                    _logger.LogTrace("[{SystemAddress}] Received system message {MessageType} {Message} for {Target}", _system.Address,
                                        sys.GetType().Name, sys, target
                                    );
                                target.SendSystemMessage(_system, sys);
                                break;
                            default:
                                Proto.MessageHeader? header = null;
                                if (envelope.MessageHeader is not null) header = new Proto.MessageHeader(envelope.MessageHeader.HeaderData);
                                var localEnvelope = new Proto.MessageEnvelope(message, sender, header);
                                if (_logger.IsEnabled(LogLevel.Trace))
                                    _logger.LogTrace("[{SystemAddress}] Received user message {MessageType} {Message} for {Target} from {Sender}",
                                        _system.Address, message.GetType().Name, message, target, sender
                                    );
                                _system.Root.Send(target, localEnvelope);
                                break;
                        }
                    }
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentMessage));
            }
        }
    }
}