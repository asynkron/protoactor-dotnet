// -----------------------------------------------------------------------
// <copyright file="ActorContextExtras.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Threading;
using Proto.Utils;

// ReSharper disable once CheckNamespace
namespace Proto;

//Angels cry over this code, but it serves a purpose, lazily init of less frequently used features
//
//Why does this exists?
//because most actors do not need any of this, it is extra state that comes at a cost
//most actors are short lived, no children. no stash, no timers
//therefore we only use this extra state when needed, to keep actors as lightweight as possible
public sealed class ActorContextExtras : IDisposable
{
    public ActorContextExtras(IContext context)
    {
        Context = context;
    }

    public ImmutableHashSet<PID> Children { get; private set; } = ImmutableHashSet<PID>.Empty;
    public Timer? ReceiveTimeoutTimer { get; private set; }
    public RestartStatistics RestartStatistics { get; } = new(0, null);
    public ImmutableHashSet<PID> Watchers { get; private set; } = ImmutableHashSet<PID>.Empty;
    public IContext Context { get; }
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    internal TypeDictionary<object, ActorContextExtras> Store { get; } = new(5, 1);

    public void Dispose()
    {
        ReceiveTimeoutTimer?.Dispose();
        ReceiveTimeoutTimer = null;

        // NOTE: We don't dispose CancellationTokenSource here on purpose, doing so causes
        // ActorSystem shutdown issues because of DefaultMailbox.PostSystemMessage doing
        // a Cancel call on that CancellationTokenSource. This can occur in cases of stopping
        // an actor twice. Given that we utilize this CancellationTokenSource only via the Cancel()
        // call, doing a Dispose() call is not required. More info can be found here:
        // https://github.com/asynkron/protoactor-dotnet/issues/1916
    }

    public void InitReceiveTimeoutTimer(Timer timer) => ReceiveTimeoutTimer = timer;

    public void ResetReceiveTimeoutTimer(TimeSpan timeout) => ReceiveTimeoutTimer?.Change(timeout, timeout);

    public void StopReceiveTimeoutTimer() => ReceiveTimeoutTimer?.Change(-1, -1);

    public void KillReceiveTimeoutTimer()
    {
        ReceiveTimeoutTimer?.Dispose();
        ReceiveTimeoutTimer = null;
    }

    public void AddChild(PID pid) => Children = Children.Add(pid);

    public void RemoveChild(PID msgWho) => Children = Children.Remove(msgWho);

    public void Watch(PID watcher) => Watchers = Watchers.Add(watcher);

    public void Unwatch(PID watcher) => Watchers = Watchers.Remove(watcher);
}