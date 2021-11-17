// -----------------------------------------------------------------------
//   <copyright file="Endpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2021 Asynkron AB All rights reserved
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
using Proto.Remote.Metrics;

namespace Proto.Remote
{
    public abstract class Endpoint : IEndpoint
    {
        internal Endpoint(string address, ActorSystem system, RemoteConfigBase remoteConfig)
        {
            Address = address;
            System = system;
            RemoteConfig = remoteConfig;
            _sender = Task.Run(RunAsync);
            _deserializationErrorLogLevel = system.Remote().Config.DeserializationErrorLogLevel;
        }
        public Channel<RemoteMessage> Outgoing { get; } = global::System.Threading.Channels.Channel.CreateUnbounded<RemoteMessage>();
        public ConcurrentStack<RemoteMessage> OutgoingStash { get; } = new();
        protected readonly ActorSystem System;
        protected readonly string Address;
        protected readonly RemoteConfigBase RemoteConfig;
        protected readonly ILogger Logger = Log.CreateLogger<Endpoint>();
        private readonly Dictionary<string, HashSet<PID>> _watchedActors = new();
        private readonly Channel<RemoteDeliver> _remoteDelivers = global::System.Threading.Channels.Channel.CreateUnbounded<RemoteDeliver>();
        private readonly object _synLock = new();
        private readonly Task _sender;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private CancellationToken CancellationToken => _cancellationTokenSource.Token;
        private readonly LogLevel _deserializationErrorLogLevel;

        public virtual async ValueTask DisposeAsync()
        {
            Logger.LogDebug($"[{System.Address}] Disposing endpoint {Address}");
            _remoteDelivers.Writer.TryComplete();
            _cancellationTokenSource.Cancel();
            Outgoing.Writer.TryComplete();
            TerminateEndpoint();
            await _sender.ConfigureAwait(false);
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
            Logger.LogDebug($"[{System.Address}] Disposed endpoint {Address}");
        }
        protected void TerminateEndpoint()
        {
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
                Logger.LogInformation("[{systemAddress}] Dropped {count} messages for {address}", System.Address, droppedMessageCount, Address);
        }

        private int DropMessagesInBatch(RemoteMessage remoteMessage)
        {
            var droppedMessageCout = 0;
            switch (remoteMessage.MessageTypeCase)
            {
                case RemoteMessage.MessageTypeOneofCase.DisconnectRequest:
                    Logger.LogWarning("[{systemAddress}] Dropping disconnect request for {address}", System.Address, Address);
                    break;
                case RemoteMessage.MessageTypeOneofCase.MessageBatch:
                    {
                        var batch = remoteMessage.MessageBatch;

                        for (var i = 0; i < batch.Targets.Count; i++)
                        {
                            batch.Targets[i].Ref(System);
                        }

                        var typeNames = batch.TypeNames.ToArray();

                        var m = System.Metrics.Get<RemoteMetrics>().RemoteDeserializedMessageCount;

                        foreach (var envelope in batch.Envelopes)
                        {
                            var target = batch.Targets[envelope.Target];
                            var sender = envelope.Sender == 0 ? null : batch.Senders[envelope.Sender - 1];

                            if (envelope.RequestId != default)
                            {
                                target = target.WithRequestId(envelope.RequestId);
                            }
                            var typeName = typeNames[envelope.TypeId];

                            if (!System.Metrics.IsNoop) m.Inc(new[] { System.Id, System.Address, typeName });

                            object message;

                            try
                            {
                                message = RemoteConfig.Serialization.Deserialize(typeName, envelope.MessageData, envelope.SerializerId);

                                // _logger.LogDebug("Received (Type) {Message}", message.GetType(), message);

                                //translate from on-the-wire representation to in-process representation
                                //this only applies to root level messages, and never on nested child messages
                                if (message is IRootSerialized serialized) message = serialized.Deserialize(System);
                            }
                            catch (Exception)
                            {
                                if (Logger.IsEnabled(_deserializationErrorLogLevel))
                                    Logger.Log(_deserializationErrorLogLevel, "[{systemAddress}] Unable to deserialize message with {Type}", System.Address, typeName);
                                continue;
                            }
                            droppedMessageCout++;

                            if (message is PoisonPill or Stop && sender is not null)
                            {
                                System.Root.Send(sender, new Terminated { Who = target, Why = TerminatedReason.AddressTerminated });
                            }
                            else if (message is Watch watch)
                            {
                                watch.Watcher.SendSystemMessage(System, new Terminated { Who = target, Why = TerminatedReason.AddressTerminated });
                            }
                            else
                            {
                                System.EventStream.Publish(new DeadLetterEvent(target, message, sender));
                                if (sender is not null)
                                    System.Root.Send(sender, new DeadLetterResponse { Target = target });
                            }
                        }
                    }
                    break;
                default:
                    break;
            }
            return droppedMessageCout;
        }

        private void ClearWatchers()
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

        public void RemoteUnwatch(PID target, Unwatch unwatch)
        {
            lock (_synLock)
            {
                if (_watchedActors.TryGetValue(target.Id, out var pidSet))
                {
                    if (pidSet.Remove(unwatch.Watcher) && pidSet.Count == 0)
                        _watchedActors.Remove(target.Id);
                }
                var w = unwatch.Watcher;
                SendMessage(target, unwatch);
            }
        }

        public void RemoteWatch(PID target, Watch watch)
        {
            lock (_synLock)
            {
                if (_watchedActors.TryGetValue(target.Id, out var pidSet)) pidSet.Add(watch.Watcher);
                else _watchedActors[target.Id] = new HashSet<PID> { watch.Watcher };
                SendMessage(target, watch);
            }
        }

        public void SendMessage(PID target, object msg)
        {
            var (message, sender, header) = Proto.MessageEnvelope.Unwrap(msg);
            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace("[{SystemAddress}] Sending message {MessageType} {Message} to {Target} from {Sender}", System.Address, message.GetType().Name, message, target, sender);
            if (sender is not null && sender.TryTranslateToProxyPID(System, Address, out var clientPID))
                sender = clientPID;
            var env = new RemoteDeliver(header!, message, target, sender!);
            if (CancellationToken.IsCancellationRequested || !_remoteDelivers.Writer.TryWrite(env))
            {
                Logger.LogWarning("[{SystemAddress}] Dropping message {MessageType} {Message} to {Target} from {Sender}", System.Address, message.GetType().Name, message, target, sender);
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
                        _watchedActors.Remove(terminated.Who.Id);
                }
                watcher.SendSystemMessage(System, terminated);
            }
        }
        private void RejectRemoteDeliver(RemoteDeliver env)
        {
            switch (env.Message)
            {
                case PoisonPill or Stop when env.Sender is not null:
                    System.Root.Send(env.Sender, new Terminated { Who = env.Target, Why = TerminatedReason.AddressTerminated });
                    break;
                case Watch watch:
                    watch.Watcher.SendSystemMessage(System, new Terminated { Who = env.Target, Why = TerminatedReason.AddressTerminated });
                    break;
                default:
                    if (env.Sender is not null)
                        System.Root.Send(env.Sender, new DeadLetterResponse { Target = env.Target });
                    else
                        System.EventStream.Publish(new DeadLetterEvent(env.Target, env.Message, env.Sender));
                    break;
            }
        }
        public async Task RunAsync()
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                try
                {
                    while (await _remoteDelivers.Reader.WaitToReadAsync(CancellationToken).ConfigureAwait(false))
                    {
                        var messages = new List<RemoteDeliver>();
                        while (_remoteDelivers.Reader.TryRead(out var remoteDeliver))
                        {
                            messages.Add(remoteDeliver);
                            if (messages.Count > RemoteConfig.EndpointWriterOptions.EndpointWriterBatchSize) break;
                        }
                        var batch = CreateBatch(messages);
                        Outgoing.Writer.TryWrite(new RemoteMessage { MessageBatch = batch });
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "[{systemAddress}] Error in RunAsync", System.Address);
                }
            }
        }
        private MessageBatch CreateBatch(IEnumerable<RemoteDeliver> m)
        {
            var envelopes = new List<MessageEnvelope>();
            var typeNames = new Dictionary<string, int>();
            var targets = new Dictionary<PID, int>();
            var targetList = new List<PID>();
            var typeNameList = new List<string>();
            var senders = new Dictionary<PID, int>();
            var senderList = new List<PID>();
            var counter = System.Metrics.Get<RemoteMetrics>().RemoteSerializedMessageCount;

            foreach (var rd in m)
            {
                var target = rd.Target;

                if (!targets.TryGetValue(target, out var targetId))
                {
                    targetId = targets[target] = targets.Count;
                    targetList.Add(target);
                }

                var sender = rd.Sender;

                if (sender != null && !senders.TryGetValue(sender, out var senderId))
                {
                    senderId = senders[sender] = senders.Count;
                    senderList.Add(sender);
                }
                else
                {
                    senderId = -1;
                }

                senderId++;

                var message = rd.Message;
                //if the message can be translated to a serialization representation, we do this here
                //this only apply to root level messages and never to nested child objects inside the message
                if (message is IRootSerializable deserialized) message = deserialized.Serialize(System);

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
                    (bytes, typeName, serializerId) = RemoteConfig.Serialization.Serialize(message);
                }
                catch (CodedOutputStream.OutOfSpaceException oom)
                {
                    Logger.LogError(oom, "Message is too large {Message}", message.GetType().Name);
                    throw;
                }
                catch (Exception x)
                {
                    Logger.LogError(x, "Serialization failed for message {Message}", message.GetType().Name);
                    throw;
                }

                if (!System.Metrics.IsNoop) counter.Inc(new[] { System.Id, System.Address, typeName });

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
                    RequestId = rd.Target.RequestId
                };
                // if (Logger.IsEnabled(LogLevel.Trace))
                //     Logger.LogTrace("[{systemAddress}] Endpoint adding Envelope {Envelope}", System.Address, envelope);
                envelopes.Add(envelope);
            }

            var batch = new MessageBatch();
            batch.Targets.AddRange(targetList);
            batch.TypeNames.AddRange(typeNameList);
            batch.Envelopes.AddRange(envelopes);
            batch.Senders.AddRange(senderList);
            // if (Logger.IsEnabled(LogLevel.Trace))
            //     Logger.LogTrace("[{systemAddress}] Sending {Count} envelopes for {Address}", System.Address, envelopes.Count, Address);
            return batch;
        }
    }
}
