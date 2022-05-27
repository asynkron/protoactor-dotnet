// -----------------------------------------------------------------------
// <copyright file="Publisher.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.PubSub;

public record ProduceMessage(object Message, TaskCompletionSource<bool> TaskCompletionSource);

/// <summary>
/// The batching producer has an internal queue collecting messages to be published to a topic. Internal loop creates and sends the batches
/// with the configured <see cref="IPublisher"/>.
/// </summary>
[PublicAPI]
public class BatchingProducer : IAsyncDisposable
{
    private static readonly ILogger Logger = Log.CreateLogger<BatchingProducer>();
    private static readonly ShouldThrottle _logThrottle = Throttle.Create(3, TimeSpan.FromSeconds(10));

    private readonly string _topic;

    private readonly Channel<ProduceMessage> _publisherChannel;
    private readonly int _batchSize;
    private CancellationTokenSource _cts = new();
    private Task _publisherLoop;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Create a new batching producer for specified topic
    /// </summary>
    /// <param name="publisher">Publish batches through this publisher</param>
    /// <param name="topic">Topic to produce to</param>
    /// <param name="batchSize">Max size of the batch</param>
    /// <param name="maxQueueSize">Max size of the requests waiting in queue. If value is provided, the producer will throw <see cref="ProducerQueueFullException"/> when queue size is exceeded. If null, the queue is unbounded.</param>
    /// <returns></returns>
    public BatchingProducer(IPublisher publisher, string topic, int batchSize = 2000, int? maxQueueSize = null)
    {
        _publisher = publisher;
        _topic = topic;
        _batchSize = batchSize;

        _publisherChannel = maxQueueSize != null
            ? Channel.CreateBounded<ProduceMessage>(maxQueueSize.Value)
            : Channel.CreateUnbounded<ProduceMessage>();

        _publisherLoop = Task.Run(() => PublisherLoop(_cts.Token));
    }

    private async Task PublisherLoop(CancellationToken cancel)
    {
        Logger.LogDebug("Producer is starting the publisher loop for topic {Topic}", _topic);

        var batch = new PublisherBatchMessage();

        try
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    if (_publisherChannel.Reader.TryRead(out var produceMessage))
                    {
                        var message = produceMessage.Message;
                        var taskCompletionSource = produceMessage.TaskCompletionSource;
                        batch.Envelopes.Add(message);
                        batch.DeliveryReports.Add(taskCompletionSource);

                        if (batch.Envelopes.Count < _batchSize) continue;

                        await PublishBatch(batch);
                        batch = new PublisherBatchMessage();
                    }
                    else
                    {
                        if (batch.Envelopes.Count > 0)
                        {
                            await PublishBatch(batch);
                            batch = new PublisherBatchMessage();
                        }

                        await _publisherChannel.Reader.WaitToReadAsync(cancel);
                    }
                }
            }
            finally
            {
                // at this point stop accepting new messages
                _publisherChannel.Writer.Complete();
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // expected, disposing
        }
        catch (Exception e)
        {
            e.CheckFailFast();
            if (_logThrottle().IsOpen())
                Logger.LogError(e, "Error in the publisher loop of Producer for topic {Topic}", _topic);

            PurgeCurrentBatch(batch, e);
            await PurgePendingMessages(e);
        }

        PurgeCurrentBatch(batch);
        await PurgePendingMessages();

        Logger.LogDebug("Producer is stopping the publisher loop for topic {Topic}", _topic);
    }

    private async Task PurgePendingMessages(Exception? ex = null)
    {
        await foreach (var producerMessage in _publisherChannel.Reader.ReadAllAsync())
        {
            if (ex != null)
                producerMessage.TaskCompletionSource.SetException(ex);
            else
                producerMessage.TaskCompletionSource.SetCanceled();
        }
    }

    private void PurgeCurrentBatch(PublisherBatchMessage batch, Exception? ex = null)
    {
        foreach (var deliveryReport in batch.DeliveryReports)
        {
            if (ex != null)
                deliveryReport.SetException(ex);
            else
                deliveryReport.SetCanceled();
        }

        batch.Envelopes.Clear();
        batch.DeliveryReports.Clear();
    }

    private async Task PublishBatch(PublisherBatchMessage batch)
    {
        //TODO: retries etc...
        await _publisher.PublishBatch(_topic, batch);

        foreach (var tcs in batch.DeliveryReports)
        {
            tcs.SetResult(true);
        }
    }

    /// <summary>
    /// Adds a message to producer queue. The returned Task will complete when the message is actually published.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown when the producer is already stopped or failed.</exception>
    /// <exception cref="ProducerQueueFullException">Thrown when producer max queue size is reached.</exception>
    public Task ProduceAsync(object message)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (!_publisherChannel.Writer.TryWrite(new ProduceMessage(message, tcs)))
        {
            if(_publisherChannel.Reader.Completion.IsCompleted)
                throw new InvalidOperationException($"This producer for topic {_topic} is stopped, cannot produce more messages.");

            throw new ProducerQueueFullException(_topic);
        }

        return tcs.Task;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        await _publisherLoop;
        _cts.Dispose();
    }
}

public class ProducerQueueFullException : Exception {
    public ProducerQueueFullException(string topic) : base($"Producer for topic {topic} has full queue") { }
}