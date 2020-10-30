namespace Proto.Cluster.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using ClusterTest.Messages;
    using Xunit;
    using Xunit.Abstractions;

    public abstract class OrderedDeliveryTests : IClassFixture<OrderedDeliveryTests.OrderedDeliveryFixture>
    {
        private readonly OrderedDeliveryFixture _clusterFixture;

        private ImmutableList<Cluster> Members => _clusterFixture.Members;

        protected OrderedDeliveryTests(OrderedDeliveryFixture clusterFixture)
        {
            _clusterFixture = clusterFixture;
        }

        [Theory]
        [InlineData(100, 10)]
        public virtual async Task OrderedDeliveryFromActors(int sendingActors, int messagesSentPerCall)
        {
            const string aggregatorId = "agg-1";

            await Task.Delay(3000);

            var maxWait = new CancellationTokenSource(5000).Token;

            var sendToRequest = new SendToRequest
            {
                Count = messagesSentPerCall,
                Id = aggregatorId
            };
            var sendRequestsSent = Members.SelectMany(
                    cluster => Enumerable.Range(0, sendingActors)
                        .Select(id => cluster.RequestAsync<Ack>($"snd-{id}", "sender", sendToRequest, maxWait))
                )
                .ToList();

            await Task.WhenAll(sendRequestsSent);

            var result = await Members.First().RequestAsync<AggregatorResult>(aggregatorId, "aggregator",
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
            private readonly ITestOutputHelper _testOutputHelper;

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

                        var key = Guid.NewGuid().ToString("N");
                        for (var i = 0; i < sendTo.Count; i++)
                        {
                            try
                            {
                                await _cluster.RequestAsync<Ack>(sendTo.Id, VerifyOrderActor.Kind,
                                    new SequentialIdRequest
                                    {
                                        SequenceKey = key,
                                        SequenceId = _seq++,
                                        Sender = _instanceId
                                    }, CancellationToken.None
                                );
                            }
                            catch (Exception e)
                            {
                                _testOutputHelper.WriteLine("Failed to send to aggregator: {0}", e);
                            }
                        }

                        context.Respond(new Ack());
                        break;
                }
            }
        }

        private class VerifyOrderActor : IActor
        {
            public const string Kind = "verify-order";

            private int _outOfOrderErrors;
            private int _seqRequests;

            private readonly Dictionary<string, int> _lastReceivedSeq = new Dictionary<string, int>();
            private readonly HashSet<string> _senders = new HashSet<string>();

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case SequentialIdRequest request:
                        HandleOrderedRequest(request, context);
                        break;
                    case AskAggregator _:
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
                var outOfOrder = _lastReceivedSeq.TryGetValue(request.SequenceKey, out var last) &&
                                 last + 1 != request.SequenceId;
                _lastReceivedSeq[request.SequenceKey] = request.SequenceId;
                if (outOfOrder)
                {
                    _outOfOrderErrors++;
                }

                context.Respond(new Ack());
            }
        }

        public class OrderedDeliveryFixture : BaseInMemoryClusterFixture
        {
            public OrderedDeliveryFixture() : base(3)
            {
            }

            protected override (string, Props)[] ClusterKinds
            {
                get
                {
                    var senderProps = Props.FromProducer(() => new SenderActor());
                    var aggProps = Props.FromProducer(() => new VerifyOrderActor());
                    return base.ClusterKinds.Concat(new[]
                        {
                            (SenderActor.Kind, senderProps),
                            (VerifyOrderActor.Kind, aggProps)
                        }
                    ).ToArray();
                }
            }
        }
    }
}