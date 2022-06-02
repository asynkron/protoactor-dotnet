﻿// -----------------------------------------------------------------------
// <copyright file = "PubSubTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Proto.Cluster.PubSub;
using Xunit;

namespace Proto.Cluster.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class PubSubBatchingProducerTests
{
    [Fact]
    public async Task Producer_sends_messages_in_batches()
    {
        await using var producer = new BatchingProducer(new MockPublisher(Record), "topic", new BatchingProducerConfig {BatchSize = 10});

        var tasks = Enumerable.Range(1, 10000)
            .Select(i => producer.ProduceAsync(new TestMessage(i)))
            .ToList();

        await Task.WhenAll(tasks);

        _batchesSent.Any(b => b.Envelopes.Count > 1).Should().BeTrue("messages should be batched");
        _batchesSent.All(b => b.Envelopes.Count <= 10).Should().BeTrue("batches should not exceed configured size");

        AllSentNumbers(_batchesSent).Should().Equal(Enumerable.Range(1, 10000), "all messages should be sent");
    }

    [Fact]
    public async Task Publishing_through_stopped_producer_throws()
    {
        var producer = new BatchingProducer(new MockPublisher(Record), "topic", new BatchingProducerConfig {BatchSize = 10});
        await producer.DisposeAsync();

        var sutAction = () => producer.ProduceAsync(new TestMessage(1));
        await sutAction.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task All_pending_tasks_complete_when_producer_is_stopped()
    {
        var producer = new BatchingProducer(new MockPublisher(Wait), "topic", new BatchingProducerConfig {BatchSize = 5});
        var tasks = Enumerable.Range(1, 100).Select(i => producer.ProduceAsync(new TestMessage(i))).ToArray();

        await producer.DisposeAsync();

        // the first batch might complete processing, so we don't verify it
        tasks.Skip(5).All(t => t.IsCompleted).Should().BeTrue("all pending messages should complete");
        tasks.Skip(5).All(t => t.IsCanceled).Should().BeTrue("all pending tasks should have status canceled");
    }

    [Fact]
    public async Task All_pending_tasks_complete_when_producer_fails()
    {
        var producer = new BatchingProducer(new MockPublisher(WaitThenFail), "topic", new BatchingProducerConfig {BatchSize = 5});
        var tasks = Enumerable.Range(1, 100).Select(i => producer.ProduceAsync(new TestMessage(i))).ToArray();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (TestException)
        {
            // this is expected since Task.WhenAll will rethrow
        }

        tasks.All(t => t.IsCompleted).Should().BeTrue("all pending messages should complete");
        tasks.All(t => t.IsFaulted).Should().BeTrue("all pending tasks should have status faulted");
    }

    [Fact]
    public async Task Publishing_through_failed_producer_throws()
    {
        await using var producer = new BatchingProducer(new MockPublisher(Fail), "topic", new BatchingProducerConfig {BatchSize = 10});
        var sutAction = () => producer.ProduceAsync(new TestMessage(1));
        await sutAction.Should().ThrowAsync<TestException>(); // here we get the exception thrown during publish

        sutAction = () => producer.ProduceAsync(new TestMessage(1));
        await sutAction.Should()
            .ThrowAsync<InvalidOperationException>(); // we get InvalidOperationException because we can no longer produce new messages
    }

    [Fact]
    public async Task Throws_when_queue_full()
    {
        await using var producer =
            new BatchingProducer(new MockPublisher(Wait), "topic", new BatchingProducerConfig {BatchSize = 1, MaxQueueSize = 10});

        var sutAction = () => {
            for (var i = 0; i < 20; i++)
            {
                _ = producer.ProduceAsync(new TestMessage(i));
            }
        };

        sutAction.Should().Throw<ProducerQueueFullException>();
    }

    [Fact(Skip="Flaky")]
    public async Task Can_cancel_publishing_a_message()
    {
        await using var producer =
            new BatchingProducer(new MockPublisher(WaitThenRecord(100)), "topic", new BatchingProducerConfig {BatchSize = 1, MaxQueueSize = 10});

        var messageWithoutCancellation = new TestMessage(1);
        var t1 = producer.ProduceAsync(messageWithoutCancellation);

        var messageWithCancellation = new TestMessage(2);
        var cts = new CancellationTokenSource();
        var t2 = producer.ProduceAsync(messageWithCancellation, cts.Token);

        cts.Cancel();

        // first message completes
        var sutAction = (() => t1);
        await sutAction.Should().NotThrowAsync();

        // second throws cancelled
        sutAction = (() => t2);
        await sutAction.Should().ThrowAsync<OperationCanceledException>();

        AllSentNumbers(_batchesSent).Should().Equal(1);
    }

    [Fact]
    public async Task Can_retry_on_publishing_error()
    {
        var retries = new List<int>();

        await using var producer =
            new BatchingProducer(new MockPublisher(FailTimesThenSucceed(3)), "topic",
                new BatchingProducerConfig
                {
                    BatchSize = 1,
                    OnPublishingError = (retry, _, _) => {
                        retries.Add(retry);
                        return Task.FromResult(PublishingErrorDecision.RetryBatchImmediately);
                    }
                }
            );

        await producer.ProduceAsync(new TestMessage(1));

        retries.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Can_skip_batch_on_publishing_error()
    {
        await using var producer =
            new BatchingProducer(new MockPublisher(FailTimesThenSucceed(1)), "topic",
                new BatchingProducerConfig
                {
                    BatchSize = 1,
                    OnPublishingError = (_, _, _) => Task.FromResult(PublishingErrorDecision.FailBatchAndContinue)
                }
            );

        var t1 = producer.ProduceAsync(new TestMessage(1));
        var t2 = producer.ProduceAsync(new TestMessage(2));

        // fist batch fails and is skipped
        var sutAction = (() => t1);
        await sutAction.Should().ThrowAsync<TestException>();

        // then processing continues, second batch succeeds
        sutAction = (() => t2);
        await sutAction.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Can_stop_producer_when_retrying_infinitely()
    {
        var producer =
            new BatchingProducer(new MockPublisher(Fail), "topic", new BatchingProducerConfig
                {
                    BatchSize = 1,
                    OnPublishingError = (_, _, _) => Task.FromResult(PublishingErrorDecision.RetryBatchImmediately)
                }
            );

        var t1 = producer.ProduceAsync(new TestMessage(1));
        // give it a moment to spin
        await Task.Delay(50);

        await producer.DisposeAsync();
        t1.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task If_message_is_cancelled_meanwhile_retrying_it_is_not_published()
    {
        var publisher = new OptionalFailureMockPublisher {ShouldFail = true};
        await using var producer = new BatchingProducer(publisher, "topic", new BatchingProducerConfig()
            {
                BatchSize = 1,
                OnPublishingError = (_, _, _) => Task.FromResult(PublishingErrorDecision.RetryBatchImmediately)
            }
        );

        var cts = new CancellationTokenSource();
        var t1 = producer.ProduceAsync(new TestMessage(1), cts.Token);

        // give it a moment to spin
        await Task.Delay(50);

        // cancel the message publish
        cts.Cancel();
        var sutAction = (() => t1);
        // message should not be published and it should complete as canceled
        await sutAction.Should().ThrowAsync<OperationCanceledException>("message should have been canceled");
        publisher.SentBatches.Should().HaveCount(0);

        // if we now stop failing the publish, next message should go through
        publisher.ShouldFail = false;
        await producer.ProduceAsync(new TestMessage(2), default);

        AllSentNumbers(publisher.SentBatches).Should().Equal(2);
    }

    private readonly List<PublisherBatchMessage> _batchesSent = new();

    private Task Record(PublisherBatchMessage batch)
    {
        var copy = new PublisherBatchMessage();
        copy.Envelopes.AddRange(batch.Envelopes);
        _batchesSent.Add(copy);

        return Task.CompletedTask;
    }

    private Task Fail(PublisherBatchMessage _) => throw new TestException();

    private Task Wait(PublisherBatchMessage _) => Task.Delay(1000);

    private async Task WaitThenFail(PublisherBatchMessage _)
    {
        await Task.Delay(500);
        throw new TestException();
    }

    private Func<PublisherBatchMessage, Task> WaitThenRecord(int ms = 500)
        => async batch => {
            await Task.Delay(ms);

            var copy = new PublisherBatchMessage();
            copy.Envelopes.AddRange(batch.Envelopes);
            _batchesSent.Add(copy);
        };

    private Func<PublisherBatchMessage, Task> FailTimesThenSucceed(int numFails)
    {
        var times = 0;

        return _ => times++ < numFails ? Task.FromException(new TestException()) : Task.CompletedTask;
    }

    private class MockPublisher : IPublisher
    {
        private readonly Func<PublisherBatchMessage, Task> _publish;

        public MockPublisher(Func<PublisherBatchMessage, Task> publish) => _publish = publish;

        public async Task<PublishResponse> PublishBatch(string topic, PublisherBatchMessage batch, CancellationToken ct = default)
        {
            await _publish(batch);
            return new PublishResponse();
        }
    }

    private class OptionalFailureMockPublisher : IPublisher
    {
        public List<PublisherBatchMessage> SentBatches { get; } = new();
        public bool ShouldFail { get; set; }

        public Task<PublishResponse> PublishBatch(string topic, PublisherBatchMessage batch, CancellationToken ct = default)
        {
            if (ShouldFail)
            {
                return Task.FromException<PublishResponse>(new TestException());
            }

            var copy = new PublisherBatchMessage();
            copy.Envelopes.AddRange(batch.Envelopes);
            SentBatches.Add(copy);
            
            return Task.FromResult(new PublishResponse());
        }
    }

    private int[] AllSentNumbers(IEnumerable<PublisherBatchMessage> batches) => batches
        .SelectMany(b => b.Envelopes)
        .Cast<TestMessage>()
        .Select(m => m.Number)
        .OrderBy(n => n)
        .ToArray();

    private record TestMessage(int Number);

    [SuppressMessage("Design", "CA1064:Exceptions should be public")]
    private class TestException : Exception
    {
    }
}