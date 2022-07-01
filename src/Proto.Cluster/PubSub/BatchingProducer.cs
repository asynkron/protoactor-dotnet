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

public record ProduceMessage(object Message, TaskCompletionSource<bool> TaskCompletionSource, CancellationToken Cancel);

/// <summary>
/// The Pub-Sub batching producer has an internal queue collecting messages to be published to a topic. Internal loop creates and sends the batches
/// with the configured <see cref="IPublisher"/>.
/// </summary>
[PublicAPI]
public class BatchingProducer : IAsyncDisposable
{
    private static readonly ILogger Logger = Log.CreateLogger<BatchingProducer>();

    private readonly string _topic;

    private readonly Channel<ProduceMessage> _publisherChannel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _publisherLoop;
    private readonly IPublisher _publisher;
    private readonly BatchingProducerConfig _config;

    /// <summary>
    /// Create a new batching producer for specified topic
    /// </summary>
    /// <param name="publisher">Publish batches through this publisher</param>
    /// <param name="topic">Topic to produce to</param>
    /// <param name="config">Producer configuration</param>
    /// <returns></returns>
    public BatchingProducer(IPublisher publisher, string topic, BatchingProducerConfig? config = null)
    {
        _publisher = publisher;
        _topic = topic;
        _config = config != null ? config with { } : new BatchingProducerConfig();

        _publisherChannel = _config.MaxQueueSize != null
            ? Channel.CreateBounded<ProduceMessage>(_config.MaxQueueSize.Value)
            : Channel.CreateUnbounded<ProduceMessage>();

        _publisherLoop = Task.Run(() => PublisherLoop(_cts.Token));
    }

    private async Task PublisherLoop(CancellationToken cancel)
    {
        Logger.LogDebug("Producer is starting the publisher loop for topic {Topic}", _topic);

        var batch = new PubSubBatch();

        try
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    if (_publisherChannel.Reader.TryRead(out var produceMessage))
                    {
                        if (!produceMessage.Cancel.IsCancellationRequested)
                        {
                            batch.Envelopes.Add(produceMessage.Message);
                            batch.DeliveryReports.Add(produceMessage.TaskCompletionSource);
                            batch.CancelTokens.Add(produceMessage.Cancel);
                        }
                        else
                        {
                            _ = produceMessage.TaskCompletionSource.TrySetCanceled(CancellationToken.None);
                        }

                        if (batch.Envelopes.Count < _config.BatchSize) continue;

                        await PublishBatch(batch);
                        batch = new PubSubBatch();
                    }
                    else
                    {
                        if (batch.Envelopes.Count > 0)
                        {
                            await PublishBatch(batch);
                            batch = new PubSubBatch();
                        }

                        await _publisherChannel.Reader.WaitToReadAsync(cancel);
                    }
                }
            }
            finally
            {
                StopAcceptingNewMessages();
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // expected, disposing
        }
        catch (Exception e)
        {
            e.CheckFailFast();
            if (_config.LogThrottle().IsOpen())
                Logger.LogError(e, "Error in the publisher loop of Producer for topic {Topic}", _topic);

            FailBatch(batch, e);
            await FailPendingMessages(e);
        }

        CancelBatch(batch);
        await CancelPendingMessages();

        Logger.LogDebug("Producer is stopping the publisher loop for topic {Topic}", _topic);
    }

    private async Task FailPendingMessages(Exception e)
    {
        await foreach (var producerMessage in _publisherChannel.Reader.ReadAllAsync())
        {
            producerMessage.TaskCompletionSource.SetException(e);
        }
    }

    private async Task CancelPendingMessages()
    {
        await foreach (var producerMessage in _publisherChannel.Reader.ReadAllAsync())
        {
            producerMessage.TaskCompletionSource.SetCanceled();
        }
    }

    private void ClearBatch(PubSubBatch batch)
    {
        batch.Envelopes.Clear();
        batch.DeliveryReports.Clear();
        batch.CancelTokens.Clear();
    }

    private void FailBatch(PubSubBatch batch, Exception ex)
    {
        foreach (var deliveryReport in batch.DeliveryReports)
        {
            deliveryReport.SetException(ex);
        }

        ClearBatch(batch);
    }

    private void CancelBatch(PubSubBatch batch)
    {
        foreach (var deliveryReport in batch.DeliveryReports)
        {
            deliveryReport.SetCanceled();
        }

        ClearBatch(batch);
    }

    private void CompleteBatch(PubSubBatch batch)
    {
        foreach (var deliveryReport in batch.DeliveryReports)
        {
            deliveryReport.SetResult(true);
        }

        ClearBatch(batch);
    }

    private void RemoveCancelledFromBatch(PubSubBatch batch)
    {
        var cancelTokensCopy = batch.CancelTokens.ToArray();

        for (var i = cancelTokensCopy.Length - 1; i >= 0; i--)
        {
            if (cancelTokensCopy[i].IsCancellationRequested)
            {
                batch.DeliveryReports[i].SetCanceled();

                batch.DeliveryReports.RemoveAt(i);
                batch.Envelopes.RemoveAt(i);
                batch.CancelTokens.RemoveAt(i);
            }
        }
    }

    private void StopAcceptingNewMessages()
    {
        if (!_publisherChannel.Reader.Completion.IsCompleted)
            _publisherChannel.Writer.Complete();
    }

    private async Task PublishBatch(PubSubBatch batch)
    {
        var retries = 0;
        var retry = true;

        while (retry && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                retries++;
                
                var response = await _publisher.PublishBatch(_topic, batch, CancellationTokens.FromSeconds(_config.PublishTimeoutInSeconds));
                if (response == null)
                    throw new TimeoutException("Timeout when publishing message batch");

                retry = false;
                CompleteBatch(batch);
            }
            catch (Exception e)
            {
                var decision = await _config.OnPublishingError(retries, e, batch);

                if (decision == PublishingErrorDecision.FailBatchAndStop)
                {
                    StopAcceptingNewMessages();
                    FailBatch(batch, e);
                    throw; // let the main producer loop exit
                }

                if (_config.LogThrottle().IsOpen())
                    Logger.LogWarning(e, "Error while publishing batch");

                if (decision == PublishingErrorDecision.FailBatchAndContinue)
                {
                    FailBatch(batch, e);
                    return;
                }

                // the decision is to retry
                // if any of the messages have been canceled in the meantime, remove them and cancel the delivery report
                RemoveCancelledFromBatch(batch);
                if (batch.IsEmpty())
                    retry = false; // no messages left in the batch, so stop retrying
                else if (decision.Delay != null)
                    await Task.Delay(decision.Delay.Value);
            }
        }

        if (_cts.Token.IsCancellationRequested)
            CancelBatch(batch);
    }

    /// <summary>
    /// Adds a message to producer queue. The returned Task will complete when the message is actually published.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="ct">If cancellation is requested before the message is published (while waiting in the queue), it will not be published,
    /// and the task returned from ProduceAsync will be cancelled</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown when the producer is already stopped or failed.</exception>
    /// <exception cref="ProducerQueueFullException">Thrown when producer max queue size is reached.</exception>
    public Task ProduceAsync(object message, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (!_publisherChannel.Writer.TryWrite(new ProduceMessage(message, tcs, ct)))
        {
            if (_publisherChannel.Reader.Completion.IsCompleted)
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

public class ProducerQueueFullException : Exception
{
    public ProducerQueueFullException(string topic) : base($"Producer for topic {topic} has full queue")
    {
    }
}