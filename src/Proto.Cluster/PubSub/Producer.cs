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
        
        private readonly string _topic;
        private readonly Channel<ProduceMessage> _publisherChannel = Channel.CreateUnbounded<ProduceMessage>();
        private readonly int _batchSize;

        public Producer(Cluster cluster, string topic)
        {
            _batchSize = cluster.Config.PubSubBatchSize;
            _cluster = cluster;
            _topic = topic;
            
            _ = Task.Run(PublisherLoop);
        }

        private async Task PublisherLoop()
        {
            var batch = new ProducerBatchMessage();
            while (true)
            {
                if (_publisherChannel.Reader.TryRead(out var produceMessage))
                {
                    var message = produceMessage.Message;
                    var taskCompletionSource = produceMessage.TaskCompletionSource;
                    batch.Envelopes.Add(message);
                    batch.DeliveryReports.Add(taskCompletionSource);

                    if (batch.Envelopes.Count < _batchSize) continue;
                    
                    await PublishBatch(batch);
                    batch = new ProducerBatchMessage();
                    
                }
                else
                {
                    if (batch.Envelopes.Count > 0)
                    {
                        await PublishBatch(batch);
                        batch = new ProducerBatchMessage();
                    }

                    await _publisherChannel.Reader.WaitToReadAsync();
                }
            }
        }

        public async Task PublishBatch(ProducerBatchMessage batch)
        {
            //TODO: retries etc...
            await _cluster.RequestAsync<PublishResponse>(_topic, "topic", batch, CancellationTokens.FromSeconds(5));

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