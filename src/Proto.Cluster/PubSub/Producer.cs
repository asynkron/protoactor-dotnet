// -----------------------------------------------------------------------
// <copyright file="Publisher.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Remote;

namespace Proto.Cluster.PubSub
{
    public record ProduceMessage(object Message, TaskCompletionSource<bool> TaskCompletionSource);
    
    [PublicAPI]
    public class Producer
    {
        private readonly Cluster _cluster;
        private ProducerBatchMessage _batch;
        private readonly string _topic;
        private readonly Channel<ProduceMessage> _publisherChannel = Channel.CreateUnbounded<ProduceMessage>();
        

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
                        var batch = _batch;
                        _batch = new ProducerBatchMessage();
                        await PublishBatch(batch);
                    }
                }
                else
                {
                    if (_batch.Envelopes.Count > 0)
                    {
                        var batch = _batch;
                        _batch = new ProducerBatchMessage();
                        await PublishBatch(batch);
                    }

                    await _publisherChannel.Reader.WaitToReadAsync();
                }
            }
        }

        public async Task PublishBatch(ProducerBatchMessage batch)
        {
            //TODO: retries etc...

            await _cluster.RequestAsync<PublishResponse>(_topic, "topic", _batch, CancellationToken.None);

            foreach (var tcs in batch.DeliveryReports)
            {
                tcs.SetResult(true);
            }
        }

        public Task ProduceAsync(object message)
        {
            var tcs = new TaskCompletionSource<bool>();
            _publisherChannel.Writer.TryWrite(new ProduceMessage(message, tcs));
            return tcs.Task;
        }
    }
}