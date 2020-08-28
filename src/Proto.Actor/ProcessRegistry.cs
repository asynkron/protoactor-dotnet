// -----------------------------------------------------------------------
//   <copyright file="ProcessRegistry.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public class ProcessRegistry
    {
        private const string NoHost = "nonhost";
        private readonly IList<Func<PID, Process>> _hostResolvers = new List<Func<PID, Process>>();
        private readonly HashedConcurrentDictionary _localProcesses = new HashedConcurrentDictionary();
        private string _host = NoHost;
        private int _port;

        private int _sequenceId;

        public ProcessRegistry(ActorSystem system) => System = system;

        public ActorSystem System { get; }

        public string Address { get; private set; } = NoHost;

        public void RegisterHostResolver(Func<PID, Process> resolver) => _hostResolvers.Add(resolver);

        public Process Get(PID pid)
        {
            if (pid.Address == NoHost || pid.Address == Address)
            {
                return _localProcesses.TryGetValue(pid.Id, out var process) ? process : System.DeadLetter;
            }

            var reff = _hostResolvers.Select(x => x(pid)).FirstOrDefault();

            if (reff == null)
            {
                throw new NotSupportedException("Unknown host");
            }

            return reff;

        }

        public Process GetLocal(string id)
            => _localProcesses.TryGetValue(id, out var process)
                ? process
                : System.DeadLetter;

        public (PID pid, bool ok) TryAdd(string id, Process process)
        {
            var pid = new PID(Address, id, process);

            var ok = _localProcesses.TryAdd(pid.Id, process);
            return ok ? (pid, true) : (new PID(Address, id), false);
        }

        public void Remove(PID pid) => _localProcesses.Remove(pid.Id);

        public string NextId()
        {
            var counter = Interlocked.Increment(ref _sequenceId);
            return "$" + counter;
        }

        public void SetAddress(string host, int port)
        {
            _host = host;
            _port = port;
            Address = $"{host}:{port}";
        }

        public (string Host, int Port) GetAddress() => (_host, _port);

        internal void Shutdown()
        {
            _host = NoHost;
            _hostResolvers.Clear();
        }
    }
}