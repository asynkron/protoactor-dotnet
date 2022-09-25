// -----------------------------------------------------------------------
// <copyright file="ProcessRegistry.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     Manages all processes in the actor system (actors, futures, event stream, etc.).
/// </summary>
public class ProcessRegistry
{
    private readonly List<Func<PID, Process?>> _hostResolvers = new();
    private readonly ConcurrentDictionary<string, Process> _localProcesses = new();
    private int _sequenceId;

    public ProcessRegistry(ActorSystem system)
    {
        System = system;
    }

    private ActorSystem System { get; }

    public int ProcessCount => _localProcesses.Count;

    public IEnumerable<PID> Find(Func<string, bool> predicate)
    {
        var res = _localProcesses.Where(kvp => predicate(kvp.Key));

        foreach (var (id, process) in res)
        {
            yield return new PID(System.Address, id, process);
        }
    }

    public IEnumerable<PID> Find(string pattern) =>
        Find(s => s.Contains(pattern, StringComparison.InvariantCultureIgnoreCase));

    public void RegisterHostResolver(Func<PID, Process> resolver) => _hostResolvers.Add(resolver);

    public Process Get(PID pid)
    {
        if (pid.Address == ActorSystem.NoHost || (pid.Address == System.Address &&
                                                  !pid.Id.StartsWith(ActorSystem.Client, StringComparison.Ordinal)))
        {
            return (_localProcesses.TryGetValue(pid.Id, out var process) switch
            {
                true  => process,
                false => System.DeadLetter
                // ReSharper disable once RedundantSuppressNullableWarningExpression
            })!;
        }

        Process? reff = null;

        foreach (var resolver in _hostResolvers)
        {
            reff = resolver(pid);

            if (reff != null)
            {
                return reff;
            }
        }

        return reff switch
        {
            null => throw new NotSupportedException("Unknown host"),
            _    => reff
        };
    }

    public (PID pid, bool ok) TryAdd(string id, Process process)
    {
        var pid = new PID(System.Address, id, process);

        var ok = _localProcesses.TryAdd(pid.Id, process);

        return ok ? (pid, true) : (PID.FromAddress(System.Address, id), false);
    }

    public void Remove(PID pid) => _localProcesses.TryRemove(pid.Id, out _);

    public string NextId()
    {
        var counter = Interlocked.Increment(ref _sequenceId);

        return "$" + counter;
    }
}