// -----------------------------------------------------------------------
// <copyright file="Publisher.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.PubSub;

/// <summary>
///     The Pub-Sub batching producer has an internal queue collecting messages to be published to a topic. Internal loop
///     creates and sends the batches
///     with the configured <see cref="IPublisher" />.
/// </summary>
[PublicAPI]
public class BatchingProducer : IAsyncDisposable
{
    private static readonly ILogger Logger = Log.CreateLogger<BatchingProducer>();
    private readonly BatchingProducerConfig _config;
    private readonly CancellationTokenSource _cts = new();
    private readonly IPublisher _publisher;

    private readonly Channel<ProduceMessage> _publisherChannel;
    private readonly Task _publisherLoop;

    private readonly string _topic;

    /// <summary>
    ///     Create a new batching producer for specified topic
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

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        await _publisherLoop.ConfigureAwait(false);
        _cts.Dispose();
    }

    private async Task PublisherLoop(CancellationToken cancel)
    {
        Logger.LogDebug("Producer is starting the publisher loop for topic {Topic}", _topic);

        var batchWrapper = new PubSubBatchWithReceipts();

        try
        {
            await _publisher.Initialize(new PublisherConfig
            {
                IdleTimeout = _config.PublisherIdleTimeout
            }, _topic, cancel).ConfigureAwait(false);
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    if (_publisherChannel.Reader.TryRead(out var produceMessage))
                    {
                        if (!produceMessage.Cancel.IsCancellationRequested)
                        {
                            batchWrapper.Batch.Envelopes.Add(produceMessage.Message);
                            batchWrapper.DeliveryReports.Add(produceMessage.TaskCompletionSource);
                            batchWrapper.CancelTokens.Add(produceMessage.Cancel);
                        }
                        else
                        {
                            _ = produceMessage.TaskCompletionSource.TrySetCanceled(CancellationToken.None);
                        }

                        if (batchWrapper.Batch.Envelopes.Count < _config.BatchSize)
                        {
                            continue;
                        }

                        await PublishBatch(batchWrapper).ConfigureAwait(false);
                        batchWrapper = new PubSubBatchWithReceipts();
                    }
                    else
                    {
                        if (batchWrapper.Batch.Envelopes.Count > 0)
                        {
                            await PublishBatch(batchWrapper).ConfigureAwait(false);
                            batchWrapper = new PubSubBatchWithReceipts();
                        }

                        await _publisherChannel.Reader.WaitToReadAsync(cancel).ConfigureAwait(false);
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
            {
                Logger.LogError(e, "Error in the publisher loop of Producer for topic {Topic}", _topic);
            }

            FailBatch(batchWrapper, e);
            await FailPendingMessages(e).ConfigureAwait(false);
        }

        CancelBatch(batchWrapper);
        await CancelPendingMessages().ConfigureAwait(false);

        Logger.LogDebug("Producer is stopping the publisher loop for topic {Topic}", _topic);
    }

    private async Task FailPendingMessages(Exception e)
    {
        await foreach (var producerMessage in _publisherChannel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            producerMessage.TaskCompletionSource.SetException(e);
        }
    }

    private async Task CancelPendingMessages()
    {
        await foreach (var producerMessage in _publisherChannel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            producerMessage.TaskCompletionSource.SetCanceled();
        }
    }

    private void ClearBatch(PubSubBatchWithReceipts batchWrapper)
    {
        // we just remove the reference to the batch
        // the batch itself might still be in send pipeline, waiting to be serialized or delivered locally
        batchWrapper.Batch = new PubSubBatch();
        batchWrapper.DeliveryReports.Clear();
        batchWrapper.CancelTokens.Clear();
    }

    private void FailBatch(PubSubBatchWithReceipts batch, Exception ex)
    {
        foreach (var deliveryReport in batch.DeliveryReports)
        {
            deliveryReport.SetException(ex);
        }

        // ensure once failed, we won't touch the batch anymore
        ClearBatch(batch);
    }

    private void CancelBatch(PubSubBatchWithReceipts batchWrapper)
    {
        foreach (var deliveryReport in batchWrapper.DeliveryReports)
        {
            deliveryReport.SetCanceled();
        }

        // ensure once cancelled, we won't touch the batch anymore
        ClearBatch(batchWrapper);
    }

    private void CompleteBatch(PubSubBatchWithReceipts batchWrapper)
    {
        foreach (var deliveryReport in batchWrapper.DeliveryReports)
        {
            deliveryReport.SetResult(true);
        }

        // ensure once completed, we won't touch the batch anymore
        ClearBatch(batchWrapper);
    }

    private void RemoveCancelledFromBatch(PubSubBatchWithReceipts batchWrapper)
    {
        var cancelTokensCopy = batchWrapper.CancelTokens.ToArray();

        for (var i = cancelTokensCopy.Length - 1; i >= 0; i--)
        {
            if (cancelTokensCopy[i].IsCancellationRequested)
            {
                batchWrapper.DeliveryReports[i].SetCanceled();

                batchWrapper.DeliveryReports.RemoveAt(i);
                batchWrapper.Batch.Envelopes.RemoveAt(i);
                batchWrapper.CancelTokens.RemoveAt(i);
            }
        }
    }

    private void StopAcceptingNewMessages()
    {
        if (!_publisherChannel.Reader.Completion.IsCompleted)
        {
            _publisherChannel.Writer.Complete();
        }
    }

    private async Task PublishBatch(PubSubBatchWithReceipts batchWrapper)
    {
        var retries = 0;
        var retry = true;

        while (retry && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                retries++;

                var response = await _publisher.PublishBatch(_topic, batchWrapper.Batch,
                    CancellationTokens.FromSeconds(_config.PublishTimeoutInSeconds)).ConfigureAwait(false);

                if (response == null)
                {
                    throw new TimeoutException("Timeout when publishing message batch");
                }

                retry = false;
                CompleteBatch(batchWrapper);
            }
            catch (Exception e)
            {
                var decision = await _config.OnPublishingError(retries, e, batchWrapper.Batch).ConfigureAwait(false);

                if (decision == PublishingErrorDecision.FailBatchAndStop)
                {
                    StopAcceptingNewMessages();
                    FailBatch(batchWrapper, e);

                    throw; // let the main producer loop exit
                }

                if (_config.LogThrottle().IsOpen())
                {
                    Logger.LogWarning(e, "Error while publishing batch");
                }

                if (decision == PublishingErrorDecision.FailBatchAndContinue)
                {
                    FailBatch(batchWrapper, e);

                    return;
                }

                // the decision is to retry
                // if any of the messages have been canceled in the meantime, remove them and cancel the delivery report
                RemoveCancelledFromBatch(batchWrapper);

                if (batchWrapper.IsEmpty())
                {
                    retry = false; // no messages left in the batch, so stop retrying
                }
                else if (decision.Delay != null)
                {
                    await Task.Delay(decision.Delay.Value).ConfigureAwait(false);
                }
            }
        }

        if (_cts.Token.IsCancellationRequested)
        {
            CancelBatch(batchWrapper);
        }
    }

    /// <summary>
    ///     Adds a message to producer queue. The returned Task will complete when the message is actually published.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="ct">
    ///     If cancellation is requested before the message is published (while waiting in the queue), it will not be
    ///     published,
    ///     and the task returned from ProduceAsync will be cancelled
    /// </param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown when the producer is already stopped or failed.</exception>
    /// <exception cref="ProducerQueueFullException">Thrown when producer max queue size is reached.</exception>
    public Task ProduceAsync(object message, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (!_publisherChannel.Writer.TryWrite(new ProduceMessage(message, tcs, ct)))
        {
            if (_publisherChannel.Reader.Completion.IsCompleted)
            {
                throw new InvalidOperationException(
                    $"This producer for topic {_topic} is stopped, cannot produce more messages.");
            }

            throw new ProducerQueueFullException(_topic);
        }

        return tcs.Task;
    }

    private record ProduceMessage(object Message, TaskCompletionSource<bool> TaskCompletionSource,
        CancellationToken Cancel);

    private class PubSubBatchWithReceipts
    {
        public PubSubBatch Batch { get; set; } = new();

        public List<TaskCompletionSource<bool>> DeliveryReports { get; } = new();

        public List<CancellationToken> CancelTokens { get; } = new();

        public bool IsEmpty() => Batch.Envelopes.Count == 0;
    }
}

#pragma warning disable RCS1194
public class ProducerQueueFullException : Exception
#pragma warning restore RCS1194
{
    public ProducerQueueFullException(string topic) : base($"Producer for topic {topic} has full queue")
    {
    }
}
