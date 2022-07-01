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
/// Manages all processes in the actor system (actors, futures, event stream, etc.).
/// </summary>
public class ProcessRegistry
{
    private readonly List<Func<PID, Process?>> _hostResolvers = new();
    private readonly ConcurrentDictionary<string, Process> _namedProcesses = new();
    private readonly ConcurrentDictionary<long, Process> _processesBySequence = new();

    // Used for prefix named spawns
    private int _sequenceId;

    public IEnumerable<PID> Find(Func<string, bool> predicate)
    {
        var res = _namedProcesses.Where(kvp => predicate(kvp.Key));

        foreach (var (id, process) in res)
        {
            yield return new PID(System.Address, id, process);
        }
    }

    public IEnumerable<PID> Find(string pattern) =>
        Find(s => s.Contains(pattern, StringComparison.InvariantCultureIgnoreCase));

    public ProcessRegistry(ActorSystem system) => System = system;

    private ActorSystem System { get; }

    public int ProcessCount => _namedProcesses.Count;

    public void RegisterHostResolver(Func<PID, Process> resolver) => _hostResolvers.Add(resolver);

    public Process Get(PID pid)
    {
        if (pid.Address == ActorSystem.NoHost || (pid.Address == System.Address && !pid.Id.StartsWith(ActorSystem.Client, StringComparison.Ordinal)))
        {
            if (pid.SequenceId > 0) // PID identified by sequential Process.Id
            {
                return (_processesBySequence.TryGetValue(pid.SequenceId, out var seqProcess) switch
                {
                    true  => seqProcess,
                    false => System.DeadLetter
                })!;
            }

            return (_namedProcesses.TryGetValue(pid.Id, out var process) switch
            {
                true  => process,
                false => System.DeadLetter
            })!;
        }

        Process? reff = null;

        foreach (var resolver in _hostResolvers)
        {
            reff = resolver(pid);
            if (reff != null) return reff;
        }

        return reff switch
        {
            null => throw new NotSupportedException("Unknown host"),
            _    => reff
        };
    }

    public PID Add(Process process)
    {
        // Processes have unique id's, so if it's already there, it's already been added.
        _processesBySequence.TryAdd(process.Id, process);
        return new PID(System.Address, process);
    }

    public (PID pid, bool ok) TryAdd(string id, Process process)
    {
        var ok = _namedProcesses.TryAdd(id, process);

        if (ok)
        {
            _processesBySequence.TryAdd(process.Id, process);
        }

        return ok
            ? (new PID(System.Address, id, process), true)
            : (new PID(System.Address, id, process.Id), false); // Does not link the process to the PID on failure.
    }

    public void Remove(PID pid)
    {
        switch (pid.SequenceId)
        {
            case 0:
                if (_namedProcesses.TryRemove(pid.Id, out var process))
                {
                    _processesBySequence.Remove(process.Id, out _);
                }

                break;
            default:
                _processesBySequence.TryRemove(pid.SequenceId, out _);

                if (!string.IsNullOrEmpty(pid.Id))
                {
                    _namedProcesses.TryRemove(pid.Id, out _);
                }

                break;
        }
    }

    public string NextId()
    {
        var counter = Interlocked.Increment(ref _sequenceId);
        return "$" + counter;
    }
}