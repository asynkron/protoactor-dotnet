// -----------------------------------------------------------------------
// <copyright file="ProcessRegistry.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
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
        private readonly List<Func<PID, Process>> _clientResolvers = new();
        private readonly List<Func<PID, Process>> _hostResolvers = new();
        private readonly HashedConcurrentDictionary _localProcesses = new();
        private int _sequenceId;

        public ProcessRegistry(ActorSystem system) => System = system;

        private ActorSystem System { get; }

        public int ProcessCount => _localProcesses.Count;

        public void RegisterHostResolver(Func<PID, Process> resolver) => _hostResolvers.Add(resolver);

        public void RegisterClientResolver(Func<PID, Process> resolver) => _clientResolvers.Add(resolver);

        public Process Get(PID pid)
        {
            if (pid.Address == ActorSystem.NoHost || pid.Address == System.Address)

            {
                if (_localProcesses.TryGetValue(pid.Id, out var process)) return process;

                var client = _clientResolvers.Select(x => x(pid)).FirstOrDefault();
                if (client is null) return System.DeadLetter;

                return client;
            }

            var reff = _hostResolvers.Select(x => x(pid)).FirstOrDefault();

            if (reff is null) throw new NotSupportedException("Unknown host");

            return reff;
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