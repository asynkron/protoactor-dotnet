// -----------------------------------------------------------------------
// <copyright file="Publisher.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Cluster.PubSub
{
    public record Foo(object Message, TaskCompletionSource<bool> TaskCompletionSource);
    public class Producer
    {
        private readonly Cluster _cluster;
        private ProducerBatch _batch;
        private readonly string _topic;
        private readonly Channel<Foo> _publisherChannel = Channel.CreateUnbounded<Foo>();
        

        public Producer(Cluster cluster, string topic)
        {
            _cluster = cluster;
            _topic = topic;
            _batch = new ProducerBatch();
            _ = Task.Run(PublisherLoop);
        }

        private async Task PublisherLoop()
        {
            var s = _cluster.System.Serialization();
            while (true)
            {
                if (_publisherChannel.Reader.TryRead(out var foo))
                {
                    var message = foo.Message;
                    var taskCompletionSource = foo.TaskCompletionSource;
                    
                    var typeName = s.GetTypeName(message,s.DefaultSerializerId);
                    var messageData = s.Serialize(message,s.DefaultSerializerId);
                    var typeIndex = _batch.TypeNames.IndexOf(typeName);
                    
                    if (typeIndex == -1)
                    {
                        _batch.TypeNames.Add(typeName);
                        typeIndex = _batch.TypeNames.Count - 1;
                    }

                    var producerMessage = new ProducerEnvelope
                    {
                        MessageData = messageData,
                        TypeId = typeIndex,
                    };
            
                    _batch.Envelopes.Add(producerMessage);
                    _batch.DeliveryReports.Add(taskCompletionSource);
                    
                    if (_batch.Envelopes.Count > 2000)
                    {
                        await PublishBatch();
                    }
                }
                else
                {
                    if (_batch.Envelopes.Count > 0)
                    {
                        await PublishBatch();
                    }

                    await _publisherChannel.Reader.WaitToReadAsync();
                }
            }
        }

        private async Task PublishBatch()
        {
            //TODO: retries etc...

            await _cluster.RequestAsync<PublishResponse>(_topic, "topic", _batch, CancellationToken.None);

            foreach (var tcs in _batch.DeliveryReports)
            {
                tcs.SetResult(true);
            }
            _batch = new ProducerBatch();
        }

        public Task ProduceAsync(object message)
        {
            var tcs = new TaskCompletionSource<bool>();
            _publisherChannel.Writer.TryWrite(new Foo(message, tcs));
            return tcs.Task;
        }
    }
}