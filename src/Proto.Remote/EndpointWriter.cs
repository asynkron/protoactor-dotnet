// -----------------------------------------------------------------------
//  <copyright file="EndpointWriter.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;

namespace Proto.Remote
{
    public class EndpointWriter : IActor
    {
        private readonly string _host;
        private Channel _channel;
        private Remoting.RemotingClient _client;
        private AsyncDuplexStreamingCall<MessageBatch, Unit> _stream;
        private IClientStreamWriter<MessageBatch> _streamWriter;

        public EndpointWriter(string host)
        {
            _host = host;
        }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started m:
                    Console.WriteLine("EndpointWriter Started");
                    await StartedAsync();
                    break;
                case Stopped m:
                    await StoppedAsync();
                    break;
                case Restarting m:
                    Console.WriteLine("EndpointWriter Restarting");
                    await RestartingAsync();
                    break;
                case IEnumerable<MessageEnvelope> m:
                    var envelopes = m;
                    await SendEnvelopesAsync(envelopes, context);
                    break;
            }
        }

        private async Task SendEnvelopesAsync(IEnumerable<MessageEnvelope> envelopes, IContext context)
        {
            var batch = new MessageBatch();
            batch.Envelopes.AddRange(envelopes);

            try
            {
                await _streamWriter.WriteAsync(batch);
            }
            catch (Exception x)
            {
                context.Stash();
                Console.WriteLine($"[REMOTING] gRPC Failed to send to host {_host}");
                throw;
            }
        }

        private async Task RestartingAsync()
        {
            await _channel.ShutdownAsync();
        }

        private async Task StoppedAsync()
        {
            await _channel.ShutdownAsync();
        }

        private Task StartedAsync()
        {
            _channel = new Channel(_host, ChannelCredentials.Insecure);
            _client = new Remoting.RemotingClient(_channel);
            _stream = _client.Receive();

            Task.Factory.StartNew(() =>
            {
                try
                {
                    var tmp = _stream.ResponseStream.Current;
                }
                catch
                {
                    Console.WriteLine($"[REMOTING] EndpointWriter lost connection to address {_host}");
                    var terminated = new EndpointTerminated
                    {
                        Address = _host
                    };
                    Actor.EventStream.Publish(terminated);
                }
            });

            _streamWriter = _stream.RequestStream;
            return Actor.Done;
        }
    }
}