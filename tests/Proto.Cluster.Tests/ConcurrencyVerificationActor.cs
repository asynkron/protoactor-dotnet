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

namespace Proto.Cluster.Tests;

public class ConcurrencyVerificationActor : IActor
{
    public const string Kind = "concurrency-verification";
    private readonly IClusterFixture _clusterFixture;

    private readonly ActorStateRepo _repo;
    private int _count;
    private ActorState? _state;

    public ConcurrencyVerificationActor(ActorStateRepo repo, IClusterFixture clusterFixture)
    {
        _repo = repo;
        _clusterFixture = clusterFixture;
    }

    private Guid SessionId { get; set; }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
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
                SessionId = SessionId.ToString("N")
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
        _state!.RecordStopping(context);
        await Task.Delay(new Random().Next(50));
    }

    private async Task OnStarted(IContext context)
    {
        SessionId = Guid.NewGuid();
        _state = _repo.Get(context.ClusterIdentity()!.Identity, _clusterFixture);
        _state.RecordStarted(context);
        _count = _state.StoredCount;

        // Simulate network hop
        await Task.Delay(new Random().Next(50));
    }
}

public record VerificationEvent(PID Activation, DateTimeOffset When);

public record ActorStarted(string Member, PID Activation, DateTimeOffset When, int StoredCount, long GlobalCount)
    : VerificationEvent(Activation, When);

public record ActorStopped(string Member, PID Activation, DateTimeOffset When, int StoredCount, long GlobalCount)
    : VerificationEvent(Activation, When);

public record ConsistencyError(
        PID Activation,
        DateTimeOffset When,
        int StoredCount,
        long GlobalCount,
        int ExpectedCount,
        int ActualCount
    )
    : VerificationEvent(Activation, When);

public record ClusterSnapshot(PID Activation, DateTimeOffset When, string Snapshot) : VerificationEvent(Activation,
    When);

public class ActorState
{
    private readonly IClusterFixture _clusterFixture;
    private readonly HashSet<string> _currentlyOnMembers = new();

    private readonly string _id;

    private readonly object _padlock = new();
    private long _totalCount;

    public ActorState(string id, IClusterFixture clusterFixture)
    {
        _id = id;
        _clusterFixture = clusterFixture;
    }

    public int StoredCount { get; set; }
    public bool Inconsistent { get; private set; }
    public long TotalCount => Interlocked.Read(ref _totalCount);
    public ConcurrentBag<VerificationEvent> Events { get; } = new();

    public void RecordStarted(IContext context)
    {
        lock (_padlock)
        {
            Events.Add(new ActorStarted(context.System.Id, context.Self, DateTimeOffset.Now, StoredCount, TotalCount));
            _currentlyOnMembers.Add(context.System.Id);

            if (!AnyOfCurrentMembersIsStopping() && _currentlyOnMembers.Count != 1)
            {
                Inconsistent = true;

                Events.Add(new ClusterSnapshot(context.Self, DateTimeOffset.Now,
                    _clusterFixture.Members.DumpClusterState().Result));
            }
        }
    }

    public void RecordStopping(IContext context)
    {
        lock (_padlock)
        {
            Events.Add(new ActorStopped(context.System.Id, context.Self, DateTimeOffset.Now, StoredCount, TotalCount));

            if (!AnyOfCurrentMembersIsStopping() && _currentlyOnMembers.Count - 1 != 0)
            {
                Inconsistent = true;

                Events.Add(new ClusterSnapshot(context.Self, DateTimeOffset.Now,
                    _clusterFixture.Members.DumpClusterState().Result));
            }

            _currentlyOnMembers.Remove(context.System.Id);
        }
    }

    // do not verify consistency if any of the current members is blocked (which means they are shutting down)
    // in this case we may see duplicated activation, but this is by design and we don't want to report it
    // activation count should go back to expected value once the duplicated activation shuts down together with the member
    private bool AnyOfCurrentMembersIsStopping() =>
        _currentlyOnMembers.Any(cm => _clusterFixture.Members.Any(m => m.Remote.BlockList.IsBlocked(cm)));

    public void RecordInconsistency(int expected, int actual, PID activation)
    {
        Events.Add(new ConsistencyError(activation, DateTimeOffset.Now, StoredCount, TotalCount, expected, actual));
        Inconsistent = true;
    }

    public (int local, long total) Inc(int actorLocalCount) =>
        (actorLocalCount + 1, Interlocked.Increment(ref _totalCount));

    public override string ToString() =>
        $"Id: {_id}, {nameof(StoredCount)}: {StoredCount}, {nameof(TotalCount)}: {TotalCount}, {nameof(Events)}:\n{EventsToString()}";

    private string EventsToString() =>
        Events
            .OrderBy(e => e.When)
            .Aggregate("", (agg, e)
                => agg + e switch
                {
                    ActorStarted started     => $"[{started.When:O}] Actor started on member {started.Member}\n",
                    ActorStopped stopped     => $"[{stopped.When:O}] Actor stopped on member {stopped.Member}\n",
                    ClusterSnapshot snapshot => $"[{snapshot.When:O}] Cluster snapshot:\n{snapshot.Snapshot}\n",
                    ConsistencyError err =>
                        $"[{err.When:O}] Consistency error, actual: {err.ActualCount}, expected: {err.ExpectedCount}, stored: {err.StoredCount}, global: {err.GlobalCount}",
                    _ => ""
                }
            );
}

public class ActorStateRepo
{
    private readonly ConcurrentDictionary<string, ActorState> _db = new();

    public ICollection<ActorState> Contents => _db.Values;

    public ActorState Get(string id, IClusterFixture fixture) =>
        _db.GetOrAdd(id, identity => new ActorState(identity, fixture));

    public void Reset() => _db.Clear();
}