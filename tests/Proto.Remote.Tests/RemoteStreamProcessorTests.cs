using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FluentAssertions;
using Grpc.Core;
using Proto.Remote;
using Proto;
using Xunit;

namespace Proto.Remote.Tests;

public class RemoteStreamProcessorTests
{
    private sealed class TestEndpoint : IEndpoint
    {
        public Channel<RemoteDeliver[]> Outgoing { get; } = Channel.CreateUnbounded<RemoteDeliver[]>();
        public ConcurrentStack<RemoteDeliver[]> OutgoingStash { get; } = new();
        public bool IsActive => true;
        public void SendMessage(PID pid, object message) { }
        public void RemoteTerminate(PID target, Terminated terminated) { }
        public void RemoteWatch(PID pid, Watch watch) { }
        public void RemoteUnwatch(PID pid, Unwatch unwatch) { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestWriter : IAsyncStreamWriter<RemoteMessage>
    {
        public List<RemoteMessage> Messages { get; } = new();
        public WriteOptions WriteOptions { get; set; }
        public Task WriteAsync(RemoteMessage message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class TestReader : IAsyncStreamReader<RemoteMessage>
    {
        private readonly IEnumerator<RemoteMessage> _enumerator;
        public TestReader(IEnumerable<RemoteMessage> messages) => _enumerator = messages.GetEnumerator();
        public RemoteMessage Current => _enumerator.Current;
        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromResult(_enumerator.MoveNext());
    }

    private sealed class ThrowingAfterFirstReader : IAsyncStreamReader<RemoteMessage>
    {
        private int _count;
        public RemoteMessage Current { get; private set; } = new();
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_count == 0)
            {
                _count++;
                return Task.FromResult(true);
            }

            throw new InvalidOperationException("Can't read messages after the request is complete.");
        }
    }

    [Fact]
    public async Task WriteLoopFlushesStash()
    {
        var system = new ActorSystem();
        var endpoint = new TestEndpoint();
        var writer = new TestWriter();
        var deliver = new RemoteDeliver(Proto.MessageHeader.Empty, new object(), new PID(), null);
        endpoint.OutgoingStash.Push(new[] { deliver });
        var cts = new CancellationTokenSource();

        try
        {
            await RemoteStreamProcessor.RunWriterAsync(
                endpoint,
                system,
                RemoteConfig.BindToLocalhost(),
                (m, ct) =>
                {
                    var task = writer.WriteAsync(m);
                    cts.Cancel();
                    return task;
                },
                cts.Token,
                cts,
                null);
        }
        catch (TaskCanceledException)
        {
            // expected due to cancellation
        }

        writer.Messages.Count.Should().Be(1);
    }

    [Fact]
    public async Task ReadLoopProcessesMessages()
    {
        var messages = new[] { new RemoteMessage(), new RemoteMessage() };
        var reader = new TestReader(messages);
        var received = new List<RemoteMessage>();
        await RemoteStreamProcessor.RunReaderAsync(reader, CancellationToken.None, m => received.Add(m));
        received.Count.Should().Be(2);
    }

    [Fact]
    public async Task ReadLoopIgnoresCompletionError()
    {
        var reader = new ThrowingAfterFirstReader();
        var received = new List<RemoteMessage>();
        await RemoteStreamProcessor.RunReaderAsync(reader, CancellationToken.None, m => received.Add(m));
        received.Count.Should().Be(1);
    }
}
