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
        private ProducerBatchMessage _batch;
        private readonly string _topic;
        private readonly Channel<Foo> _publisherChannel = Channel.CreateUnbounded<Foo>();
        

        public Producer(Cluster cluster, string topic)
        {
            _cluster = cluster;
            _topic = topic;
            _batch = new ProducerBatchMessage();
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
                    _batch.Envelopes.Add(message);
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
            _batch = new ProducerBatchMessage();
        }

        public Task ProduceAsync(object message)
        {
            var tcs = new TaskCompletionSource<bool>();
            _publisherChannel.Writer.TryWrite(new Foo(message, tcs));
            return tcs.Task;
        }
    }
}