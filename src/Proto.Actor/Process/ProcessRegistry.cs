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
    private readonly ConcurrentDictionary<string,Process> _localProcesses = new();
    private readonly ConcurrentDictionary<long,Process> _localProcesses2 = new();
    private long _sequenceId = 1;
        
    public IEnumerable<PID> Find(Func<string, bool> predicate)
    {
        var res = _localProcesses.Where(kvp => predicate(kvp.Key));

        foreach (var (id, process) in res)
        {
            yield return new PID(System.Address, id, process); //TODO: fix sequence id
        }
    }

    public IEnumerable<PID> Find(string pattern) => 
        Find(s => s.Contains(pattern, StringComparison.InvariantCultureIgnoreCase));

    public ProcessRegistry(ActorSystem system) => System = system;

    private ActorSystem System { get; }

    public int ProcessCount => _localProcesses.Count;

    public void RegisterHostResolver(Func<PID, Process> resolver) => _hostResolvers.Add(resolver);

    public Process Get(PID pid)
    {
        if (pid.Address == ActorSystem.NoHost || (pid.Address == System.Address && !pid.Id.StartsWith(ActorSystem.Client, StringComparison.Ordinal)))
        {
            if (pid.SequenceId != 0)
            {
                if (_localProcesses2.TryGetValue(pid.SequenceId, out var process))
                {
                    return process;
                }
            }

            if (!string.IsNullOrEmpty(pid.Id))
            {
                if (_localProcesses.TryGetValue(pid.Id, out var process2))
                {
                    return process2;
                }
            }

            return System.DeadLetter;
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

    public (PID pid, bool ok) TryAdd(string id, Process process)
    {
        var sequenceId = Interlocked.Increment(ref _sequenceId);
        var pid = new PID(System.Address, id, process, sequenceId);

        var ok = true;

        //only add to named registry if id exists
        if (!string.IsNullOrEmpty(id))
        {
            ok = _localProcesses.TryAdd(pid.Id, process);
        }

        //always add to long registry
        _localProcesses2.TryAdd(sequenceId, process);
        
        return ok ? (pid, true) : (PID.FromAddress(System.Address, id), false);
    }

    public void Remove(PID pid)
    {
        if (!string.IsNullOrEmpty(pid.Id))
        {
            _localProcesses.TryRemove(pid.Id, out _);
        }

        _localProcesses2.TryRemove(pid.SequenceId, out _);
    }

    public string NextId()
    {
        var counter = Interlocked.Increment(ref _sequenceId);
        return "$" + counter;
    }
}