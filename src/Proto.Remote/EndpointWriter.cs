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
        private int _serializerId;
        private readonly string _address;
        private readonly CallOptions _callOptions;
        private readonly ChannelCredentials _channelCredentials;
        private readonly IEnumerable<ChannelOption> _channelOptions;
        private readonly ILogger _logger = Log.CreateLogger<EndpointWriter>();
        private Channel _channel;
        private Remoting.RemotingClient _client;
        private AsyncDuplexStreamingCall<MessageBatch, Unit> _stream;
        private IClientStreamWriter<MessageBatch> _streamWriter;

        public EndpointWriter(string address, IEnumerable<ChannelOption> channelOptions, CallOptions callOptions, ChannelCredentials channelCredentials)
        {
            _address = address;
            _channelOptions = channelOptions;
            _callOptions = callOptions;
            _channelCredentials = channelCredentials;
        }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    await StartedAsync();
                    break;
                case Stopped _:
                    await StoppedAsync();
                    _logger.LogDebug($"Stopped EndpointWriter at {_address}");
                    break;
                case Restarting _:
                    await RestartingAsync();
                    break;
                case EndpointTerminatedEvent _:
                    context.Self.Stop();
                    break;
                case IEnumerable<RemoteDeliver> m:
                    var envelopes = new List<MessageEnvelope>();
                    var typeNames = new Dictionary<string,int>();
                    var targetNames = new Dictionary<string,int>();
                    var typeNameList = new List<string>();
                    var targetNameList = new List<string>();
                    foreach(var rd in m)
                    {
                        var targetName = rd.Target.Id;
                        var serializerId = rd.SerializerId == -1 ? _serializerId : rd.SerializerId;

                        if (!targetNames.TryGetValue(targetName, out var targetId))
                        {
                            targetId = targetNames[targetName] = targetNames.Count;
                            targetNameList.Add(targetName);
                        }

                        var typeName = Serialization.GetTypeName(rd.Message, serializerId);
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

                        var bytes = Serialization.Serialize(rd.Message, serializerId);
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

                    await SendEnvelopesAsync(batch, context);
                    break;
            }
        }

        private async Task SendEnvelopesAsync(MessageBatch batch, IContext context)
        {
            try
            {
                await _streamWriter.WriteAsync(batch);
            }
            catch (Exception x)
            {
                context.Stash();
                _logger.LogError($"gRPC Failed to send to address {_address}, reason {x.Message}");
                throw;
            }
        }

        //shutdown channel before restarting
        private Task RestartingAsync() => _channel.ShutdownAsync();

        //shutdown channel before stopping
        private Task StoppedAsync() => _channel.ShutdownAsync();

        private async Task StartedAsync()
        {
            _logger.LogDebug($"Connecting to address {_address}");
            _channel = new Channel(_address, _channelCredentials, _channelOptions);
            _client = new Remoting.RemotingClient(_channel);

            try
            {
                var res = await _client.ConnectAsync(new ConnectRequest());
                _serializerId = res.DefaultSerializerId;
                _stream = _client.Receive(_callOptions);
                _streamWriter = _stream.RequestStream;
            }
            catch(Exception ex)
            {
                _logger.LogError($"GRPC Failed to connect to address {_address}\n{ex}");
                //Wait for 2 seconds to restart and retry
                //Replace with Exponential Backoff
                await Task.Delay(2000);
                throw;
            }

            var _ = Task.Factory.StartNew(async () =>
            {
                try
                {
                    await _stream.ResponseStream.ForEachAsync(i => Actor.Done);
                }
                catch (Exception x)
                {
                    _logger.LogError($"Lost connection to address {_address}, reason {x.Message}");
                    var terminated = new EndpointTerminatedEvent
                    {
                        Address = _address
                    };
                    Actor.EventStream.Publish(terminated);
                }
            });

            var connected = new EndpointConnectedEvent
            {
                Address = _address
            };
            Actor.EventStream.Publish(connected);

            _logger.LogDebug($"Connected to address {_address}");
        }
    }
}