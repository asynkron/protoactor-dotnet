// -----------------------------------------------------------------------
// <copyright file="ProcessRegistry.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public class ProcessRegistry
    {
        private readonly List<Func<PID, Process>> _hostResolvers = new();
        private readonly HashedConcurrentDictionary _localProcesses = new();
        private int _sequenceId;
        
        public IEnumerable<PID> Find(Func<string, bool> predicate)
        {
            var res = _localProcesses.Where(kvp => predicate(kvp.key));

            foreach (var (id, process) in res)
            {
                yield return new PID(System.Address, id, process);
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
                if (_localProcesses.TryGetValue(pid.Id, out var process)) return process;
                return System.DeadLetter;
            }
            else
            {
                Process? reff = null;
                foreach (var resolver in _hostResolvers)
                {
                    reff = resolver(pid);
                    if (reff != null) return reff;
                }

                if (reff is null) throw new NotSupportedException("Unknown host");
                return reff;
            }
        }

        public (PID pid, bool ok) TryAdd(string id, Process process)
        {
            var pid = new PID(System.Address, id, process);

            var ok = _localProcesses.TryAdd(pid.Id, process);
            return ok ? (pid, true) : (PID.FromAddress(System.Address, id), false);
        }

        public void Remove(PID pid) => _localProcesses.Remove(pid.Id);

        public string NextId()
        {
            var counter = Interlocked.Increment(ref _sequenceId);
            return "$" + counter;
        }
    }
}