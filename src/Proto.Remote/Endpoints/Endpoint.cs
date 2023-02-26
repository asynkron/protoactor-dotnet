// -----------------------------------------------------------------------
//   <copyright file="Endpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Proto.Extensions;
using Proto.Remote.Metrics;

namespace Proto.Remote;

public abstract class Endpoint : IEndpoint
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly LogLevel _deserializationErrorLogLevel;
    private readonly ILogger _logger = Log.CreateLogger<Endpoint>();
    private readonly Channel<RemoteDeliver> _remoteDelivers = Channel.CreateUnbounded<RemoteDeliver>();
    private readonly Task _sender;
    private readonly object _synLock = new();
    private readonly Dictionary<string, HashSet<PID>> _watchedActors = new();
    protected readonly string RemoteAddress;
    protected readonly RemoteConfigBase RemoteConfig;
    protected readonly ActorSystem System;

    internal Endpoint(string remoteAddress, ActorSystem system, RemoteConfigBase remoteConfig)
    {
        RemoteAddress = remoteAddress;
        System = system;
        RemoteConfig = remoteConfig;
        _sender = Task.Run(RunAsync);
        _deserializationErrorLogLevel = system.Remote().Config.DeserializationErrorLogLevel;
    }

    private CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public Channel<RemoteMessage> Outgoing { get; } = Channel.CreateBounded<RemoteMessage>(3);
    public ConcurrentStack<RemoteMessage> OutgoingStash { get; } = new();

    public virtual async ValueTask DisposeAsync()
    {
        _logger.LogDebug("[{SystemAddress}] Disposing endpoint {Address}", System.Address, RemoteAddress);
        _remoteDelivers.Writer.TryComplete();
        _cancellationTokenSource.Cancel();
        Outgoing.Writer.TryComplete();
        TerminateEndpoint();
        await _sender.ConfigureAwait(false);
        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
        _logger.LogDebug("[{SystemAddress}] Disposed endpoint {Address}", System.Address, RemoteAddress);
    }

    public bool IsActive { get; private set; } = true;

    public void RemoteUnwatch(PID target, Unwatch unwatch)
    {
        lock (_synLock)
        {
            if (_watchedActors.TryGetValue(target.Id, out var pidSet))
            {
                if (pidSet.Remove(unwatch.Watcher) && pidSet.Count == 0)
                {
                    _watchedActors.Remove(target.Id);
                }
            }

            var w = unwatch.Watcher;
            SendMessage(target, unwatch);
        }
    }

    public void RemoteWatch(PID target, Watch watch)
    {
        lock (_synLock)
        {
            if (_watchedActors.TryGetValue(target.Id, out var pidSet))
            {
                pidSet.Add(watch.Watcher);
            }
            else
            {
                _watchedActors[target.Id] = new HashSet<PID> { watch.Watcher };
            }

            SendMessage(target, watch);
        }
    }

    public void SendMessage(PID target, object msg)
    {
        var (message, sender, header) = Proto.MessageEnvelope.Unwrap(msg);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[{SystemAddress}] Sending message {MessageType} {Message} to {Target} from {Sender}",
                System.Address,
                message.GetMessageTypeName(), message, target, sender
            );
        }

        if (sender is not null && sender.TryTranslateToProxyPID(System, RemoteAddress, out var clientPID))
        {
            sender = clientPID;
        }

        var env = new RemoteDeliver(header, message, target, sender);

        if (CancellationToken.IsCancellationRequested || !_remoteDelivers.Writer.TryWrite(env))
        {
            _logger.LogWarning("[{SystemAddress}] Dropping message {MessageType} {Message} to {Target} from {Sender}",
                System.Address,
                message.GetMessageTypeName(), message, target, sender
            );

            RejectRemoteDeliver(env);
        }
    }

    public void RemoteTerminate(PID watcher, Terminated terminated)
    {
        lock (_synLock)
        {
            if (_watchedActors.TryGetValue(terminated.Who.Id, out var pidSet))
            {
                if (pidSet.Remove(watcher) && pidSet.Count == 0)
                {
                    _watchedActors.Remove(terminated.Who.Id);
                }
            }

            watcher.SendSystemMessage(System, terminated);
        }
    }

    private void TerminateEndpoint()
    {
        IsActive = false;
        ClearWatchers();
        var droppedMessageCount = 0;

        while (OutgoingStash.TryPop(out var remoteMessage))
        {
            droppedMessageCount += DropMessagesInBatch(remoteMessage);
        }

        while (Outgoing.Reader.TryRead(out var remoteMessage))
        {
            droppedMessageCount += DropMessagesInBatch(remoteMessage);
        }

        while (_remoteDelivers.Reader.TryRead(out var rd))
        {
            RejectRemoteDeliver(rd);
            droppedMessageCount++;
        }

        if (droppedMessageCount > 0)
        {
            _logger.LogInformation("[{SystemAddress}] Dropped {Count} messages for {Address}", System.Address,
                droppedMessageCount, RemoteAddress);
        }
    }

    private int DropMessagesInBatch(RemoteMessage remoteMessage)
    {
        var droppedMessageCount = 0;

        switch (remoteMessage.MessageTypeCase)
        {
            case RemoteMessage.MessageTypeOneofCase.DisconnectRequest:
                _logger.LogWarning("[{SystemAddress}] Dropping disconnect request for {Address}", System.Address,
                    RemoteAddress);

                break;
            case RemoteMessage.MessageTypeOneofCase.MessageBatch:
            {
                var batch = remoteMessage.MessageBatch;

                var targets = new PID[batch.Targets.Count];

                for (var i = 0; i < batch.Targets.Count; i++)
                {
                    var target = new PID(System.Address, batch.Targets[i]);

                    if (target.TryTranslateToLocalClientPID(out var pid))
                    {
                        targets[i] = pid;
                    }
                    else
                    {
                        targets[i] = target;
                        target.Ref(System);
                    }
                }

                for (var i = 0; i < batch.Senders.Count; i++)
                {
                    var s = batch.Senders[i];

                    if (string.IsNullOrEmpty(s.Address))
                    {
                        s.Address = RemoteAddress;
                    }

                    s.Ref(System);
                }

                var typeNames = batch.TypeNames.ToArray();

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

                    if (System.Metrics.Enabled)
                    {
                        RemoteMetrics.RemoteDeserializedMessageCount.Add(1,
                            new KeyValuePair<string, object?>("id", System.Id),
                            new KeyValuePair<string, object?>("address", System.Address),
                            new KeyValuePair<string, object?>("messagetype", typeName)
                        );
                    }

                    object message;

                    try
                    {
                        message = RemoteConfig.Serialization.Deserialize(typeName, envelope.MessageData,
                            envelope.SerializerId);

                        // _logger.LogDebug("Received (Type) {Message}", message.GetType(), message);

                        //translate from on-the-wire representation to in-process representation
                        //this only applies to root level messages, and never on nested child messages
                        if (message is IRootSerialized serialized)
                        {
                            message = serialized.Deserialize(System);
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
                                System.Address,
                                typeName
                            );
                        }

                        continue;
                    }

                    droppedMessageCount++;

                    if (message is PoisonPill or Stop && sender is not null)
                    {
                        System.Root.Send(sender,
                            new Terminated { Who = target, Why = TerminatedReason.AddressTerminated });
                    }
                    else if (message is Watch watch)
                    {
                        watch.Watcher.SendSystemMessage(System,
                            new Terminated { Who = target, Why = TerminatedReason.AddressTerminated });
                    }
                    else
                    {
                        System.EventStream.Publish(new DeadLetterEvent(target, message, sender));

                        if (sender is not null)
                        {
                            System.Root.Send(sender, new DeadLetterResponse { Target = target });
                        }
                    }
                }
            }

                break;
        }

        return droppedMessageCount;
    }

    private void ClearWatchers()
    {
        lock (_synLock)
        {
            foreach (var (id, pidSet) in _watchedActors)
            {
                var watcherPid = PID.FromAddress(System.Address, id);
                var watcherRef = System.ProcessRegistry.Get(watcherPid);

                if (watcherRef == System.DeadLetter)
                {
                    continue;
                }

                foreach (var t in pidSet.Select(
                             pid => new Terminated
                             {
                                 Who = pid,
                                 Why = TerminatedReason.AddressTerminated
                             }
                         ))
                {
                    //send the address Terminated event to the Watcher
                    watcherPid.SendSystemMessage(System, t);
                }
            }

            _watchedActors.Clear();
        }
    }

    private void RejectRemoteDeliver(RemoteDeliver env)
    {
        switch (env.Message)
        {
            case PoisonPill or Stop when env.Sender is not null:
                System.Root.Send(env.Sender,
                    new Terminated { Who = env.Target, Why = TerminatedReason.AddressTerminated });

                break;
            case Watch watch:
                watch.Watcher.SendSystemMessage(System,
                    new Terminated { Who = env.Target, Why = TerminatedReason.AddressTerminated });

                break;
            default:
                if (env.Sender is not null)
                {
                    System.Root.Send(env.Sender, new DeadLetterResponse { Target = env.Target });
                }
                else
                {
                    System.EventStream.Publish(new DeadLetterEvent(env.Target, env.Message, env.Sender));
                }

                break;
        }
    }

    private async Task RunAsync()
    {
        while (!CancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = new List<RemoteDeliver>(RemoteConfig.EndpointWriterOptions.EndpointWriterBatchSize);

                while (await _remoteDelivers.Reader.WaitToReadAsync(CancellationToken).ConfigureAwait(false))
                {
                    while (_remoteDelivers.Reader.TryRead(out var remoteDeliver))
                    {
                        messages.Add(remoteDeliver);

                        if (messages.Count >= RemoteConfig.EndpointWriterOptions.EndpointWriterBatchSize)
                        {
                            break;
                        }
                    }

                    var batch = CreateBatch(messages);
                    await Outgoing.Writer.WriteAsync(new RemoteMessage { MessageBatch = batch }, CancellationToken).ConfigureAwait(false);
                    messages.Clear();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ex.CheckFailFast();
                _logger.LogError(ex, "[{SystemAddress}] Error in RunAsync", System.Address);
            }
        }
    }

    private MessageBatch CreateBatch(IReadOnlyCollection<RemoteDeliver> m)
    {
        var envelopes = new List<MessageEnvelope>(m.Count);
        var typeNames = new Dictionary<string, int>();
        var targets = new Dictionary<(string address, string id), int>();
        var targetList = new List<string>();
        var typeNameList = new List<string>();
        var senders = new Dictionary<(string address, string id), int>();
        var senderList = new List<PID>();

        foreach (var rd in m)
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

                    senderList.Add(sender.Address == System.Address
                        ? PID.FromAddress("", sender.Id)
                        : PID.FromAddress(sender.Address, sender.Id));
                }
            }

            var message = rd.Message;

            //if the message can be translated to a serialization representation, we do this here
            //this only apply to root level messages and never to nested child objects inside the message
            if (message is IRootSerializable deserialized)
            {
                message = deserialized.Serialize(System);
            }

            if (message is null)
            {
                _logger.LogError("Null message passed to EndpointActor, ignoring message");

                continue;
            }

            ByteString bytes;
            string typeName;
            int serializerId;

            try
            {
                (bytes, typeName, serializerId) = RemoteConfig.Serialization.Serialize(message);
            }
            catch (CodedOutputStream.OutOfSpaceException oom)
            {
                System.Diagnostics.RegisterEvent("Remote", $"Message is too large {message.GetMessageTypeName()}");
                _logger.LogError(oom, "Message is too large {Message}", message.GetMessageTypeName());

                throw;
            }
            catch (Exception x)
            {
                System.Diagnostics.RegisterEvent("Remote", $"Missing serializer for {message.GetMessageTypeName()}");
                _logger.LogError(x, "Serialization failed for message {Message}", message.GetMessageTypeName());

                throw;
            }

            if (System.Metrics.Enabled)
            {
                RemoteMetrics.RemoteSerializedMessageCount.Add(1,
                    new KeyValuePair<string, object?>("id", System.Id),
                    new KeyValuePair<string, object?>("address", System.Address),
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
            //     Logger.LogTrace("[{SystemAddress}] Endpoint adding Envelope {Envelope}", System.Address, envelope);
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
        //     Logger.LogTrace("[{SystemAddress}] Sending {Count} envelopes for {Address}", System.Address, envelopes.Count, Address);
        return batch;
    }
}