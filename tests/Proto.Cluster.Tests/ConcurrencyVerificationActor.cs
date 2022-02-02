// -----------------------------------------------------------------------
// <copyright file="StatefulActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace Proto.Cluster.Tests
{
    public class ConcurrencyVerificationActor : IActor
    {
        public const string Kind = "concurrency-verification";

        private readonly ActorStateRepo _repo;
        private ActorState? _state;
        private int _count;

        public ConcurrencyVerificationActor(ActorStateRepo repo) => _repo = repo;

        private Guid SessionId { get; set; }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            Started  => OnStarted(context),
            Stopping => OnStopping(context),
            Die      => StopNow(context),
            IncCount => OnInc(context),
            _        => Task.CompletedTask
        };

        private Task OnInc(IContext context)
        {
            var (count, totalCount) = _state!.Inc(_count);
            context.Respond(new IncResponse
                {
                    Count = count,
                    ExpectedCount = (int)totalCount,
                    SessionId = SessionId.ToString("N"),
                }
            );

            if (totalCount == count) // Expected == actual
            {
                _count = count;
            }
            else
            {
                _count = (int)totalCount; // Reset to the global total count.
                _state.RecordInconsistency(count, (int)totalCount, context.Self);
            }

            _state.StoredCount = _count;

            return Task.CompletedTask;
        }

        private Task StopNow(IContext context)
        {
            context.Respond(new Ack());
            context.Stop(context.Self);
            return Task.CompletedTask;
        }

        private async Task OnStopping(IContext context)
        {
            _state!.RecordStopping(context.Self);
            await Task.Delay(new Random().Next(50));
        }

        private async Task OnStarted(IContext context)
        {
            SessionId = Guid.NewGuid();
            _state = _repo.Get(context.ClusterIdentity()!.Identity);
            _state.RecordStarted(context.Self);
            _count = _state.StoredCount;

            // Simulate network hop
            await Task.Delay(new Random().Next(50));
        }
    }

    public record VerificationEvent(PID Activation, DateTimeOffset When);

    public record ActorStarted(PID Activation, DateTimeOffset When, int StoredCount, long GlobalCount) : VerificationEvent(Activation, When);

    public record ActorStopped(PID Activation, DateTimeOffset When, int StoredCount, long GlobalCount) : VerificationEvent(Activation, When);

    public record ConsistencyError(
            PID Activation,
            DateTimeOffset When,
            int StoredCount,
            long GlobalCount,
            int ExpectedCount,
            int ActualCount
        )
        : VerificationEvent(Activation, When);

    public class ActorState
    {
        private int _activeCount;
        private long _totalCount;
        public int StoredCount { get; set; }
        public bool Inconsistent { get; private set; }
        public long TotalCount => Interlocked.Read(ref _totalCount);
        private readonly string _id;

        public ActorState(string id) => _id = id;

        public ConcurrentBag<VerificationEvent> Events { get; } = new();

        public void RecordStarted(PID activation)
        {
            Events.Add(new ActorStarted(activation, DateTimeOffset.Now, StoredCount, TotalCount));
            var active = Interlocked.Increment(ref _activeCount);

            if (active != 1)
            {
                Inconsistent = true;
            }
        }

        public void RecordStopping(PID activation)
        {
            Events.Add(new ActorStopped(activation, DateTimeOffset.Now, StoredCount, TotalCount));
            var active = Interlocked.Decrement(ref _activeCount);
            if (active != 0)
            {
                Inconsistent = true;
            }
        }

        public void RecordInconsistency(int expected, int actual, PID activation)
        {
            Events.Add(new ConsistencyError(activation, DateTimeOffset.Now, StoredCount, TotalCount, expected, actual));
            Inconsistent = true;
        }

        public (int local, long total) Inc(int actorLocalCount) => (actorLocalCount + 1, Interlocked.Increment(ref _totalCount));

        // public void VerifyStateIsConsistent()
        // {
        //     Events.Should().NotBeEmpty();
        //     using var events = Events.OrderBy(it => it.When).GetEnumerator();
        //
        //     // Check ordering of starts / stops for identity
        //     while (events.MoveNext())
        //     {
        //         var sessionId = events.Current!.SessionId;
        //         events.Current.Should().BeOfType<ActorStarted>();
        //         events.MoveNext().Should().BeTrue("We should get the stopping event");
        //         events.Current!.Should().BeOfType<ActorStopped>();
        //         events.Current.SessionId.Should().Be(sessionId, "Start and stop should be from the same session");
        //     }
        // }

        public override string ToString()
            => $"Id: {_id}, {nameof(StoredCount)}: {StoredCount}, {nameof(TotalCount)}: {TotalCount}, {nameof(Events)}:\n {string.Join(",\n", Events.OrderBy(it => it.When))}";
    }

    public class ActorStateRepo
    {
        private readonly ConcurrentDictionary<string, ActorState> _db = new();

        public ActorState Get(string id) => _db.GetOrAdd(id, identity => new ActorState(identity));

        public ICollection<ActorState> Contents => _db.Values;

        public void Reset() => _db.Clear();
    }
}