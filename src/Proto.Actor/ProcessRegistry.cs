// -----------------------------------------------------------------------
//   <copyright file="ProcessRegistry.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Proto
{
    public class ProcessRegistry
    {
        private const string NoHost = "nonhost";
        private readonly IList<Func<PID, Process>> _hostResolvers = new List<Func<PID, Process>>();
        private readonly HashedConcurrentDictionary _localActorRefs = new HashedConcurrentDictionary();
        private string _host = NoHost;
        private int _port;

        private int _sequenceId;

        public ProcessRegistry(ActorSystem system) => System = system;

        public ActorSystem System { get; }

        public string Address { get; private set; } = NoHost;

        public void RegisterHostResolver(Func<PID, Process> resolver) => _hostResolvers.Add(resolver);

        public Process Get(PID pid)
        {
            if (pid.Address != NoHost && pid.Address != Address)
            {
                var reff = _hostResolvers.Select(x => x(pid)).FirstOrDefault();

                if (reff == null)
                {
                    throw new NotSupportedException("Unknown host");
                }

                return reff;
            }

            if (_localActorRefs.TryGetValue(pid.Id, out var process))
            {
                return process;
            }

            return System.DeadLetter;
        }

        public Process GetLocal(string id)
            => _localActorRefs.TryGetValue(id, out var process)
                ? process
                : System.DeadLetter;

        public (PID pid, bool ok) TryAdd(string id, Process process)
        {
            var pid = new PID(Address, id, process);

            var ok = _localActorRefs.TryAdd(pid.Id, process);
            return ok ? (pid, true) : (new PID(Address, id), false);
        }

        public void Remove(PID pid) => _localActorRefs.Remove(pid.Id);

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
    }
}