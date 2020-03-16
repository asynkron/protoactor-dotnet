// -----------------------------------------------------------------------
//   <copyright file="EndpointWriter.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class EndpointWriter : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointWriter>();

        private int _serializerId;
        private readonly string _address;
        private readonly CallOptions _callOptions;
        private readonly ChannelCredentials _channelCredentials;
        private readonly IEnumerable<ChannelOption> _channelOptions;

        private Channel _channel;
        private Remoting.RemotingClient _client;
        private AsyncDuplexStreamingCall<MessageBatch, Unit> _stream;
        private IClientStreamWriter<MessageBatch> _streamWriter;
        private readonly ActorSystem _system;
        private readonly Serialization _serialization;

        public EndpointWriter(
            ActorSystem system,
            Serialization serialization,
            string address,
            IEnumerable<ChannelOption> channelOptions,
            CallOptions callOptions,
            ChannelCredentials channelCredentials
        )
        {
            _system = system;
            _serialization = serialization;
            _address = address;
            _channelOptions = channelOptions;
            _callOptions = callOptions;
            _channelCredentials = channelCredentials;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    Logger.LogDebug("Starting Endpoint Writer");
                    return StartedAsync();
                case Stopped _:
                    return StoppedAsync().ContinueWith(_ => Logger.LogDebug("Stopped EndpointWriter at {Address}", _address));
                case Restarting _:
                    return RestartingAsync();
                case EndpointTerminatedEvent _:
                    context.Stop(context.Self);
                    return Actor.Done;
                case IEnumerable<RemoteDeliver> m:
                    return Deliver(m);
                default:
                    return Actor.Done;
            }

            Task Deliver(IEnumerable<RemoteDeliver> m)
            {
                var envelopes = new List<MessageEnvelope>();
                var typeNames = new Dictionary<string, int>();
                var targetNames = new Dictionary<string, int>();
                var typeNameList = new List<string>();
                var targetNameList = new List<string>();

                foreach (var rd in m)
                {
                    var targetName = rd.Target.Id;
                    var serializerId = rd.SerializerId == -1 ? _serializerId : rd.SerializerId;

                    if (!targetNames.TryGetValue(targetName, out var targetId))
                    {
                        targetId = targetNames[targetName] = targetNames.Count;
                        targetNameList.Add(targetName);
                    }

                    var typeName = _serialization.GetTypeName(rd.Message, serializerId);

                    if (!typeNames.TryGetValue(typeName, out var typeId))
                    {
                        typeId = typeNames[typeName] = typeNames.Count;
                        typeNameList.Add(typeName);
                    }

                    MessageHeader header = null;

                    if (rd.Header != null && rd.Header.Count > 0)
                    {
                        header = new MessageHeader();
                        header.HeaderData.Add(rd.Header.ToDictionary());
                    }

                    var bytes = _serialization.Serialize(rd.Message, serializerId);

                    var envelope = new MessageEnvelope
                    {
                        MessageData = bytes,
                        Sender = rd.Sender,
                        Target = targetId,
                        TypeId = typeId,
                        SerializerId = serializerId,
                        MessageHeader = header,
                    };

                    envelopes.Add(envelope);
                }

                var batch = new MessageBatch();
                batch.TargetNames.AddRange(targetNameList);
                batch.TypeNames.AddRange(typeNameList);
                batch.Envelopes.AddRange(envelopes);

                Logger.LogDebug(
                    "EndpointWriter sending {Count} envelopes for {Address} while channel status is {State}",
                    envelopes.Count, _address, _channel?.State
                );

                return SendEnvelopesAsync(batch, context);
            }
        }

        private async Task SendEnvelopesAsync(MessageBatch batch, IContext context)
        {
            if (_streamWriter == null)
            {
                Logger.LogError("gRPC Failed to send to address {Address}, reason No Connection available", _address);
                return;

                // throw new EndpointWriterException("gRPC Failed to send, reason No Connection available");
            }

            try
            {
                Logger.LogDebug("Writing batch to {Address}", _address);

                await _streamWriter.WriteAsync(batch);
            }
            catch (Exception x)
            {
                context.Stash();
                Logger.LogError("gRPC Failed to send to address {Address}, reason {Message}", _address, x.Message);
                throw;
            }
        }

        //shutdown channel before restarting
        private Task RestartingAsync() => ShutDownChannel();

        //shutdown channel before stopping
        private Task StoppedAsync() => ShutDownChannel();

        private Task ShutDownChannel()
        {
            if (_channel != null && _channel.State != ChannelState.Shutdown)
            {
                return _channel.ShutdownAsync();
            }

            return Actor.Done;
        }

        private async Task StartedAsync()
        {
            Logger.LogDebug("Connecting to address {Address}", _address);

            _channel = new Channel(_address, _channelCredentials, _channelOptions);
            _client = new Remoting.RemotingClient(_channel);

            Logger.LogDebug("Created channel and client for address {Address}", _address);

            var res = await _client.ConnectAsync(new ConnectRequest());
            _serializerId = res.DefaultSerializerId;
            _stream = _client.Receive(_callOptions);
            _streamWriter = _stream.RequestStream;

            Logger.LogDebug("Connected client for address {Address}", _address);

            var _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await _stream.ResponseStream.ForEachAsync(i => Actor.Done);
                    }
                    catch (Exception x)
                    {
                        Logger.LogError("Lost connection to address {Address}, reason {Message}", _address, x.Message);

                        var terminated = new EndpointTerminatedEvent
                        {
                            Address = _address
                        };
                        _system.EventStream.Publish(terminated);
                    }
                }
            );

            Logger.LogDebug("Created reader for address {Address}", _address);

            var connected = new EndpointConnectedEvent
            {
                Address = _address
            };
            _system.EventStream.Publish(connected);

            Logger.LogDebug("Connected to address {Address}", _address);
        }
    }

    class EndpointWriterException : Exception
    {
        public EndpointWriterException(string message) : base(message) { }
    }
}