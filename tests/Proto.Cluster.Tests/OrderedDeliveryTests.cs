﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    public class OrderedDeliveryTests : ClusterTestBase, IClassFixture<OrderedDeliveryTests.OrderedDeliveryFixture>
    {
        public OrderedDeliveryTests(ITestOutputHelper testOutputHelper, OrderedDeliveryFixture clusterFixture) : base(
            clusterFixture
        )
        {
        }

        [Theory]
        [InlineData(1000, 10, 8000)]
        public async Task OrderedDeliveryFromActors(int sendingActors, int messagesSentPerCall, int timeoutMs)
        {
            string aggregatorId = CreateIdentity("agg-1");

            CancellationToken timeout = new CancellationTokenSource(timeoutMs).Token;

            SendToRequest sendToRequest = new() {Count = messagesSentPerCall, Id = aggregatorId};
            List<Task<Ack>> sendRequestsSent = Members.SelectMany(
                    cluster => GetActorIds(sendingActors)
                        .Select(id => cluster.RequestAsync<Ack>(id, SenderActor.Kind, sendToRequest, timeout))
                )
                .ToList();

            await Task.WhenAll(sendRequestsSent);

            AggregatorResult result = await Members.First().RequestAsync<AggregatorResult>(aggregatorId,
                VerifyOrderActor.Kind,
                new AskAggregator(),
                new CancellationTokenSource(5000).Token
            );

            result.Should().NotBeNull("We expect a response from the aggregator actor");
            result.SequenceKeyCount.Should().Be(sendRequestsSent.Count, "We expect a unique id per send request");
            result.SenderKeyCount.Should().Be(sendingActors, "We expect a single instantiation per sender id");
            result.OutOfOrderCount.Should().Be(0, "Messages from one actor to another should be received in order");
            result.TotalMessages.Should().Be(sendRequestsSent.Count * messagesSentPerCall);
        }

        private class SenderActor : IActor
        {
            public const string Kind = "sender";

            private Cluster _cluster;
            private string _instanceId;
            private int _seq;

            public async Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case ClusterInit init:
                        _instanceId = $"{init.Kind}:{init.Identity}.{Guid.NewGuid():N}";
                        _cluster = init.Cluster;
                        break;
                    case SendToRequest sendTo:

                        string key = Guid.NewGuid().ToString("N");

                        for (int i = 0; i < sendTo.Count; i++)
                        {
                            await _cluster.RequestAsync<Ack>(sendTo.Id, VerifyOrderActor.Kind,
                                new SequentialIdRequest {SequenceKey = key, SequenceId = _seq++, Sender = _instanceId},
                                CancellationToken.None
                            );
                        }

                        context.Respond(new Ack());
                        break;
                }
            }
        }

        private class VerifyOrderActor : IActor
        {
            public const string Kind = "aggregator";

            private readonly Dictionary<string, int> _lastReceivedSeq = new();
            private readonly HashSet<string> _senders = new();

            private int _outOfOrderErrors;
            private int _seqRequests;

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case SequentialIdRequest request:
                        HandleOrderedRequest(request, context);
                        break;
                    case AskAggregator:
                        context.Respond(new AggregatorResult
                            {
                                SequenceKeyCount = _lastReceivedSeq.Count,
                                TotalMessages = _seqRequests,
                                OutOfOrderCount = _outOfOrderErrors,
                                SenderKeyCount = _senders.Count
                            }
                        );
                        break;
                }

                return Task.CompletedTask;
            }

            private void HandleOrderedRequest(SequentialIdRequest request, IContext context)
            {
                _seqRequests++;
                _senders.Add(request.Sender);
                bool outOfOrder = _lastReceivedSeq.TryGetValue(request.SequenceKey, out int last) &&
                                  last + 1 != request.SequenceId;
                _lastReceivedSeq[request.SequenceKey] = request.SequenceId;
                if (outOfOrder)
                {
                    _outOfOrderErrors++;
                }

                context.Respond(new Ack());
            }
        }

        // ReSharper disable once ClassNeverInstantiated.Global
        public class OrderedDeliveryFixture : BaseInMemoryClusterFixture
        {
            public OrderedDeliveryFixture() : base(3)
            {
            }

            protected override ClusterKind[] ClusterKinds
            {
                get
                {
                    Props senderProps = Props.FromProducer(() => new SenderActor());
                    Props aggProps = Props.FromProducer(() => new VerifyOrderActor());
                    return base.ClusterKinds.Concat(new ClusterKind[]
                        {
                            new(SenderActor.Kind, senderProps), new(VerifyOrderActor.Kind, aggProps)
                        }
                    ).ToArray();
                }
            }
        }
    }
}
