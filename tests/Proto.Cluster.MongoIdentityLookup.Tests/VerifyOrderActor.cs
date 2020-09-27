using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;

namespace Proto.Cluster.MongoIdentityLookup.Tests
{
    /// <summary>
    /// Expects each keyed message to be received with a sequential id.
    /// </summary>
    public class VerifyOrderActor : IActor
    {
        private int _outOfOrderErrors = 0;
        private int _seqRequests = 0;

        private readonly Dictionary<string, int> _lastReceivedSeq = new Dictionary<string, int>();

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
                        DifferentKeys = _lastReceivedSeq.Count,
                        TotalMessages = _seqRequests,
                        OutOfOrder = _outOfOrderErrors
                    });
                    break;
            }

            return Actor.Done;
        }

        private void HandleOrderedRequest(SequentialIdRequest request, IContext context)
        {
            _seqRequests++;
            var outOfOrder = _lastReceivedSeq.TryGetValue(request.Key, out var last) && last + 1 != request.SequenceId;
            _lastReceivedSeq[request.Key] = request.SequenceId;
            if (outOfOrder)
            {
                _outOfOrderErrors++;
                Console.WriteLine("OOO");
            }

            context.Respond(!outOfOrder);
        }
    }
}