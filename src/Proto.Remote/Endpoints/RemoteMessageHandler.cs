// -----------------------------------------------------------------------
//   <copyright file="RemoteMessageHandler.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Proto.Extensions;
using Proto.Mailbox;
using Proto.Remote.Metrics;

namespace Proto.Remote;

public sealed class RemoteMessageHandler
{
    private readonly LogLevel _deserializationErrorLogLevel;
    private readonly EndpointManager _endpointManager;
    private readonly ILogger _logger = Log.CreateLogger<RemoteMessageHandler>();
    private readonly Serialization _serialization;
    private readonly ActorSystem _system;
    private readonly IRootContext _sendContext;

    public RemoteMessageHandler(EndpointManager endpointManager, ActorSystem system, Serialization serialization,
        RemoteConfigBase remoteConfig)
    {
        _endpointManager = endpointManager;
        _system = system;
        _serialization = serialization;
        _deserializationErrorLogLevel = remoteConfig.DeserializationErrorLogLevel;
        
        //important, must be undecorated context to not mess up e.g. Otel
        _sendContext = new RootContext(system);
    }

    public void HandleRemoteMessage(RemoteMessage currentMessage, string remoteAddress)
    {
        switch (currentMessage.MessageTypeCase)
        {
            case RemoteMessage.MessageTypeOneofCase.MessageBatch:
            {
                var batch = currentMessage.MessageBatch;
                var targets = new PID[batch.Targets.Count];

                for (var i = 0; i < batch.Targets.Count; i++)
                {
                    var target = new PID(_system.Address, batch.Targets[i]);

                    if (target.TryTranslateToLocalClientPID(out var pid))
                    {
                        targets[i] = pid;
                    }
                    else
                    {
                        targets[i] = target;
                        target.Ref(_system);
                    }
                }

                for (var i = 0; i < batch.Senders.Count; i++)
                {
                    var s = batch.Senders[i];

                    if (string.IsNullOrEmpty(s.Address))
                    {
                        s.Address = remoteAddress;
                    }

                    s.Ref(_system);
                }

                var typeNames = batch.TypeNames.ToArray();

                Counter<long>? m = null;

                if (_system.Metrics.Enabled)
                {
                    m = RemoteMetrics.RemoteDeserializedMessageCount;
                }

                foreach (var envelope in batch.Envelopes)
                {
                    var target = targets[envelope.Target];

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
                    {
                        m!.Add(1, new KeyValuePair<string, object?>("id", _system.Id),
                            new KeyValuePair<string, object?>("address", _system.Address),
                            new KeyValuePair<string, object?>("messagetype", typeName));
                    }

                    object message;

                    try
                    {
                        message = _serialization.Deserialize(typeName, envelope.MessageData, envelope.SerializerId);

                        //translate from on-the-wire representation to in-process representation
                        //this only applies to root level messages, and never on nested child messages
                        if (message is IRootSerialized serialized)
                        {
                            message = serialized.Deserialize(_system);
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.CheckFailFast();

                        if (_logger.IsEnabled(_deserializationErrorLogLevel))
                        {
                            _logger.Log(
                                _deserializationErrorLogLevel,
                                ex,
                                "[{SystemAddress}] Unable to deserialize message with {Type}",
                                _system.Address,
                                typeName);
                        }

                        continue;
                    }

                    switch (message)
                    {
                        case Terminated msg:
                            if (_logger.IsEnabled(LogLevel.Trace))
                            {
                                _logger.LogTrace(
                                    "[{SystemAddress}] Received message {MessageType} {Message} for {Target}",
                                    _system.Address,
                                    msg.GetType().Name, msg, target
                                );
                            }

                            var endpoint = msg.Who.TryGetSystemId(_system, out var systemId)
                                ? _endpointManager.GetClientEndpoint(systemId)
                                : _endpointManager.GetServerEndpoint(msg.Who.Address);

                            endpoint.RemoteTerminate(target, msg);

                            break;
                        case SystemMessage sys:
                            if (_logger.IsEnabled(LogLevel.Trace))
                            {
                                _logger.LogTrace(
                                    "[{SystemAddress}] Received system message {MessageType} {Message} for {Target}",
                                    _system.Address,
                                    sys.GetType().Name, sys, target
                                );
                            }

                            target.SendSystemMessage(_system, sys);

                            break;
                        default:
                            Proto.MessageHeader? header = null;

                            if (envelope.MessageHeader is not null)
                            {
                                header = new Proto.MessageHeader(envelope.MessageHeader.HeaderData);
                            }

                            object? messageOrEnvelope = null;

                            if (header == null && sender == null)
                            {
                                messageOrEnvelope = message;
                            }
                            else
                            {
                                messageOrEnvelope = new Proto.MessageEnvelope(message, sender, header);
                            }

                            if (_logger.IsEnabled(LogLevel.Trace))
                            {
                                _logger.LogTrace(
                                    "[{SystemAddress}] Received user message {MessageType} {Message} for {Target} from {Sender}",
                                    _system.Address, message.GetMessageTypeName(), message, target, sender
                                );
                            }

                            _sendContext.Send(target, messageOrEnvelope);

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